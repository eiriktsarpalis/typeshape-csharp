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

    public static bool IsTupleType(this Type type)
    {
        if (type.Assembly != typeof(ValueTuple<int>).Assembly || !type.IsGenericType)
        {
            return false;
        }

        Debug.Assert(type.FullName != null);
        string fullName = type.FullName;
        return fullName.StartsWith("System.ValueTuple`") || fullName.StartsWith("System.Tuple`");
    }

    public static bool IsValueTupleType(this Type type)
        => type.IsTupleType() && type.IsValueType;

    public static bool IsNestedTupleRepresentation(this Type type)
    {
        if (!type.IsTupleType())
        {
            return false;
        }

        Type[] genericArguments = type.GetGenericArguments();
        return genericArguments.Length == 8 && 
            genericArguments[7].IsValueType == type.IsValueType &&
            genericArguments[7].IsTupleType();
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

    public static IEnumerable<(string LogicalName, MemberInfo Member, MemberInfo[] ParentMembers)> EnumerateTupleMemberPaths(Type tupleType)
    {
        // Walks the nested tuple representation, returning every element field and the parent "Rest" fields needed to access the value.
        Debug.Assert(tupleType.IsTupleType());
        List<MemberInfo> nestedMembers = new();
        bool hasNestedTuple;
        int i = 0;

        do
        {
            MemberInfo[] parentMembers = tupleType.IsValueType 
                ? nestedMembers.OfType<FieldInfo>().ToArray()
                : nestedMembers.OfType<PropertyInfo>().ToArray();

            MemberInfo[] elements = tupleType.IsValueType
                ? tupleType.GetFields(BindingFlags.Instance | BindingFlags.Public)
                : tupleType.GetProperties(BindingFlags.Instance | BindingFlags.Public);

            hasNestedTuple = false;

            foreach (MemberInfo element in elements.OrderBy(e => e.Name))
            {
                if (element.Name is "Rest")
                {
                    Type memberType = element.MemberType();
                    if (memberType.IsTupleType())
                    {
                        nestedMembers.Add(element);
                        tupleType = memberType;
                        hasNestedTuple = true;
                    }
                    else
                    {
                        yield return ("Rest", element, parentMembers);
                    }
                }
                else
                {
                    yield return ($"Item{++i}", element, parentMembers);
                }
            }

        } while (hasNestedTuple);
    }

    public static ConstructorShapeInfo CreateNestedTupleConstructorShapeInfo(Type tupleType)
    {
        Debug.Assert(tupleType.IsNestedTupleRepresentation());
        return CreateCore(tupleType, offset: 0);
        static ConstructorShapeInfo CreateCore(Type tupleType, int offset)
        {
            Debug.Assert(tupleType.IsTupleType());
            ConstructorInfo ctorInfo = tupleType.GetConstructors()[0];
            ParameterInfo[] parameters = ctorInfo.GetParameters();
            ConstructorParameterShapeInfo[] ctorParameterInfo;
            ConstructorShapeInfo? nestedCtor;

            if (parameters.Length == 8 && parameters[7].ParameterType.IsTupleType())
            {
                ctorParameterInfo = MapParameterInfo(parameters.Take(7));
                nestedCtor = CreateCore(parameters[7].ParameterType, offset);
            }
            else
            {
                ctorParameterInfo = MapParameterInfo(parameters);
                nestedCtor = null;
            }

            return new ConstructorShapeInfo(tupleType, ctorInfo, ctorParameterInfo, nestedTupleCtor: nestedCtor);

            ConstructorParameterShapeInfo[] MapParameterInfo(IEnumerable<ParameterInfo> parameters)
                => parameters.Select(p => new ConstructorParameterShapeInfo(p, logicalName: $"Item{++offset}")).ToArray();
        }
    }
}
