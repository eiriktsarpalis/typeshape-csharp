using System.Diagnostics;
using System.Reflection;

namespace TypeShape.ReflectionProvider;

internal static class ReflectionHelpers
{
    public static bool IsNullable<T>()
    {
        return default(T) is null && typeof(T).IsValueType;
    }

    public static bool IsNullable(this Type type)
    {
        return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);
    }

    public static bool CanBeGenericArgument(this Type type)
    {
        return !(type == typeof(void) || type.IsPointer || type.IsByRef || type.IsByRefLike || type.ContainsGenericParameters);
    }

    public static bool IsRecord(this Type type)
        => type.GetMethod("<Clone>$", BindingFlags.Public | BindingFlags.Instance) is not null;

    public static bool IsCompilerGenerated(this MemberInfo memberInfo)
        => memberInfo.CustomAttributes.Any(static attr => attr.AttributeType.FullName == "System.Runtime.CompilerServices.CompilerGeneratedAttribute");

    public static bool IsAutoPropertyWithSetter(this MemberInfo memberInfo)
        => memberInfo is PropertyInfo { SetMethod: { } setter } && setter.IsCompilerGenerated();

    public static bool IsInitOnly(this MemberInfo memberInfo)
    {
        return memberInfo is PropertyInfo { SetMethod: { } setter } &&
            setter.ReturnParameter
                .GetRequiredCustomModifiers()
                .Any(static modifier => modifier.FullName == "System.Runtime.CompilerServices.IsExternalInit");
    }

    public static bool IsRequired(this MemberInfo memberInfo)
    {
        Debug.Assert(memberInfo is FieldInfo or PropertyInfo);
        return memberInfo.CustomAttributes.Any(static attr => attr.AttributeType.FullName == "System.Runtime.CompilerServices.RequiredMemberAttribute");
    }

    public static bool SetsRequiredMembers(this ConstructorInfo ctorInfo)
    {
        return ctorInfo.CustomAttributes.Any(static attr => attr.AttributeType.FullName == "System.Diagnostics.CodeAnalysis.SetsRequiredMembersAttribute");
    }

    public static Type MemberType(this MemberInfo memberInfo)
    {
        Debug.Assert(memberInfo is FieldInfo or PropertyInfo);
        return memberInfo is FieldInfo f ? f.FieldType : ((PropertyInfo)memberInfo).PropertyType;
    }

    public static bool TryGetDefaultValueNormalized(this ParameterInfo parameterInfo, out object? result)
    {
        if (!parameterInfo.HasDefaultValue)
        {
            result = null;
            return false;
        }

        Type parameterType = parameterInfo.ParameterType;
        object? defaultValue = parameterInfo.DefaultValue;

        if (defaultValue is null)
        {
            // ParameterInfo can report null defaults for value types, ignore such cases.
            result = null;
            return !parameterType.IsValueType || parameterType.IsNullable();
        }

        Debug.Assert(defaultValue is not DBNull, "should have been caught by the HasDefaultValue check.");

        if (parameterType.IsEnum)
        {
            defaultValue = Enum.ToObject(parameterType, defaultValue);
        }
        else if (Nullable.GetUnderlyingType(parameterType) is Type underlyingType && underlyingType.IsEnum)
        {
            defaultValue = Enum.ToObject(underlyingType, defaultValue);
        }
        else if (parameterType == typeof(IntPtr))
        {
            defaultValue = checked((IntPtr)Convert.ToInt64(defaultValue));
        }
        else if (parameterType == typeof(UIntPtr))
        {
            defaultValue = checked((UIntPtr)Convert.ToUInt64(defaultValue));
        }

        result = defaultValue;
        return true;
    }

    public static Type[] GetSortedTypeHierarchy(this Type type)
    {
        if (!type.IsInterface)
        {
            // No need to walk the class hierarchy in reflection,
            // all members are reported in the current type.
            return new[] { type };
        }
        else
        {
            // Interface hierarchies support multiple inheritance.
            // For consistency with class hierarchy resolution order,
            // sort topologically from most derived to least derived.
            return CommonHelpers.TraverseGraphWithTopologicalSort(type, static t => t.GetInterfaces());
        }
    }

    public static bool IsValueTupleType(this Type type)
    {
        if (type.Assembly != typeof(ValueTuple<int>).Assembly && !type.IsGenericType)
        {
            return false;
        }

        Debug.Assert(type.FullName != null);
        return type.FullName.StartsWith("System.ValueTuple`");
    }

    public static bool IsNestedValueTupleRepresentation(this Type type)
    {
        if (!type.IsValueTupleType())
        {
            return false;
        }

        Type[] genericArguments = type.GetGenericArguments();
        return genericArguments.Length == 8 && genericArguments[7].IsValueTupleType();
    }

    public static Type CreateValueTupleType(Type[] elementTypes)
    {
        return elementTypes.Length switch
        {
            0 => typeof(ValueTuple),
            1 => typeof(ValueTuple<>).MakeGenericType(elementTypes),
            2 => typeof(ValueTuple<,>).MakeGenericType(elementTypes),
            3 => typeof(ValueTuple<,,>).MakeGenericType(elementTypes),
            4 => typeof(ValueTuple<,,,>).MakeGenericType(elementTypes),
            5 => typeof(ValueTuple<,,,,>).MakeGenericType(elementTypes),
            6 => typeof(ValueTuple<,,,,,>).MakeGenericType(elementTypes),
            7 => typeof(ValueTuple<,,,,,,>).MakeGenericType(elementTypes),
            _ => typeof(ValueTuple<,,,,,,,>).MakeGenericType(elementTypes[..7].Append(CreateValueTupleType(elementTypes[7..])).ToArray()),
        };
    }

    public static IEnumerable<(string LogicalName, FieldInfo FieldInfo, FieldInfo[] ParentFields)> EnumerateTupleFieldPaths(Type tupleType)
    {
        // Walks the nested ValueTuple representation, returning every element field and the parent "Rest" fields needed to access the value.
        Debug.Assert(tupleType.IsValueTupleType());
        List<FieldInfo> nestedFields = new();
        bool hasNestedTuple;
        int i = 0;

        do
        {
            FieldInfo[] parentFields = nestedFields.ToArray();
            FieldInfo[] elements = tupleType.GetFields(BindingFlags.Instance | BindingFlags.Public);
            hasNestedTuple = false;

            foreach (FieldInfo element in elements.OrderBy(e => e.Name))
            {
                if (element.Name is "Rest")
                {
                    if (element.FieldType.IsValueTupleType())
                    {
                        nestedFields.Add(element);
                        tupleType = element.FieldType;
                        hasNestedTuple = true;
                    }
                    else
                    {
                        yield return ("Rest", element, parentFields);
                    }
                }
                else
                {
                    yield return ($"Item{++i}", element, parentFields);
                }
            }

        } while (hasNestedTuple);
    }

    public static ConstructorShapeInfo BuildValueTupleConstructorShapeInfo(Type tupleType)
    {
        Debug.Assert(tupleType.IsNestedValueTupleRepresentation());
        return BuildCore(tupleType, offset: 0);
        static ConstructorShapeInfo BuildCore(Type tupleType, int offset)
        {
            Debug.Assert(tupleType.IsValueTupleType());
            ConstructorInfo ctorInfo = tupleType.GetConstructors()[0];
            ParameterInfo[] parameters = ctorInfo.GetParameters();
            ConstructorParameterInfo[] ctorParameterInfo;
            ConstructorShapeInfo? nestedCtor;

            if (parameters.Length == 8 && parameters[7].ParameterType.IsValueTupleType())
            {
                ctorParameterInfo = MapParameterInfo(parameters.Take(7));
                nestedCtor = BuildCore(parameters[7].ParameterType, offset);
            }
            else
            {
                ctorParameterInfo = MapParameterInfo(parameters);
                nestedCtor = null;
            }

            return new ConstructorShapeInfo(tupleType, ctorInfo, ctorParameterInfo, nestedTupleCtor: nestedCtor);

            ConstructorParameterInfo[] MapParameterInfo(IEnumerable<ParameterInfo> parameters)
                => parameters.Select(p => new ConstructorParameterInfo(p, logicalName: $"Item{++offset}")).ToArray();
        }
    }
}
