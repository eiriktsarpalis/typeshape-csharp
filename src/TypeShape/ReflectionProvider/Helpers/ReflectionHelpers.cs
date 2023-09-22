using System.Diagnostics;
using System.Globalization;
using System.Reflection;

namespace TypeShape.ReflectionProvider;

internal static class ReflectionHelpers
{
    public readonly static Type? Int128Type = typeof(int).Assembly.GetType("System.Int128");
    public readonly static Type? UInt128Type = typeof(int).Assembly.GetType("System.UInt128");

    public static bool IsNullable<T>()
    {
        return default(T) is null && typeof(T).IsValueType;
    }

    public static bool IsNullable(this Type type)
    {
        return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);
    }

    public static bool IsIEnumerable(this Type type)
    {
        return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>);
    }

    public static void GetNonNullableReferenceInfo(this MemberInfo memberInfo, out bool isGetterNonNullable, out bool isSetterNonNullable)
    {
        if (GetNullabilityInfo(memberInfo) is NullabilityInfo info)
        {
            isGetterNonNullable = info.ReadState is NullabilityState.NotNull;
            isSetterNonNullable = info.WriteState is NullabilityState.NotNull;
        }
        else
        {
            isGetterNonNullable = false;
            isSetterNonNullable = false;
        }
    }

    public static bool IsNonNullableReferenceType(this ParameterInfo parameterInfo)
    {
        if (GetNullabilityInfo(parameterInfo) is NullabilityInfo info)
        {
            // Workaround for https://github.com/dotnet/runtime/issues/92487
            if (parameterInfo.Member.TryGetGenericMethodDefinition() is MethodBase genericMethod &&
                genericMethod.GetParameters()[parameterInfo.Position] is { ParameterType: { IsGenericParameter: true } typeParam })
            {
                Attribute? attr = typeParam.GetCustomAttributes().FirstOrDefault(attr =>
                {
                    Type attrType = attr.GetType();
                    return attrType.Namespace == "System.Runtime.CompilerServices" && attrType.Name == "NullableAttribute";
                });

                byte[]? nullableFlags = (byte[])attr?.GetType().GetField("NullableFlags")?.GetValue(attr)!;
                return nullableFlags[0] == 1;
            }

            return info.WriteState is NullabilityState.NotNull;
        }
        else
        {
            return false;
        }
    }

    public static MethodBase? TryGetGenericMethodDefinition(this MemberInfo methodBase)
    {
        Debug.Assert(methodBase is MethodInfo or ConstructorInfo);

        if (methodBase.DeclaringType!.IsGenericType)
        {
            Type genericTypeDef = methodBase.DeclaringType.GetGenericTypeDefinition();
            MethodBase[] methods = methodBase.MemberType is MemberTypes.Constructor
                ? genericTypeDef.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                : genericTypeDef.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

            MethodBase match = methods.First(m => m.MetadataToken == methodBase.MetadataToken);
            return ReferenceEquals(match, methodBase) ? null : match;
        }

        if (methodBase is MethodInfo { IsGenericMethod: true } methodInfo)
        {
            return methodInfo.GetGenericMethodDefinition();
        }

        return null;
    }

    private static NullabilityInfo? GetNullabilityInfo(ICustomAttributeProvider memberInfo)
    {
        Debug.Assert(memberInfo is PropertyInfo or FieldInfo or ParameterInfo);

        switch (memberInfo)
        {
            case PropertyInfo prop:
                return prop.PropertyType.IsValueType ? null : new NullabilityInfoContext().Create(prop);

            case FieldInfo field:
                return field.FieldType.IsValueType ? null : new NullabilityInfoContext().Create(field);

            case ParameterInfo parameter:
                return parameter.ParameterType.IsValueType ? null : new NullabilityInfoContext().Create(parameter);

            default:
                return null;
        }
    }

    public static bool CanBeGenericArgument(this Type type)
    {
        return !(type == typeof(void) || type.IsPointer || type.IsByRef || type.IsByRefLike || type.ContainsGenericParameters);
    }

    public static object Invoke(this MethodBase methodBase, params object?[]? args)
    {
        Debug.Assert(methodBase is ConstructorInfo or MethodBase { IsStatic: true });
        object? result = methodBase is ConstructorInfo ctor
            ? ctor.Invoke(args)
            : methodBase.Invoke(null, args);

        Debug.Assert(result != null);
        return result;
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

    public static IEnumerable<Type> GetAllInterfaces(this Type type)
    {
        if (type.IsInterface)
        {
            yield return type;
        }

        foreach (Type interfaceType in type.GetInterfaces())
        {
            yield return interfaceType;
        }
    }

    public static bool ImplementsInterface(this Type type, Type interfaceType)
    {
        Debug.Assert(interfaceType.IsInterface && !interfaceType.IsConstructedGenericType);

        foreach (Type otherInterface in type.GetAllInterfaces())
        {
            if (otherInterface.IsGenericType
                ? otherInterface.GetGenericTypeDefinition() == interfaceType
                : otherInterface == interfaceType)
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsExplicitInterfaceImplementation(this MethodInfo methodInfo)
    {
        Debug.Assert(!methodInfo.IsStatic);
        return methodInfo.IsPrivate && methodInfo.IsVirtual && methodInfo.Name.Contains('.');
    }

    public static bool IsExplicitInterfaceImplementation(this PropertyInfo propertyInfo)
    {
        return 
            propertyInfo.GetMethod?.IsExplicitInterfaceImplementation() == true ||
            propertyInfo.SetMethod?.IsExplicitInterfaceImplementation() == true ;
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
            defaultValue = checked((IntPtr)Convert.ToInt64(defaultValue, CultureInfo.InvariantCulture));
        }
        else if (parameterType == typeof(UIntPtr))
        {
            defaultValue = checked((UIntPtr)Convert.ToUInt64(defaultValue, CultureInfo.InvariantCulture));
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
        return fullName.StartsWith("System.ValueTuple`", StringComparison.Ordinal) || 
            fullName.StartsWith("System.Tuple`", StringComparison.Ordinal);
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

    public static TupleConstructorShapeInfo CreateNestedTupleConstructorShapeInfo(Type tupleType)
    {
        Debug.Assert(tupleType.IsNestedTupleRepresentation());
        return CreateCore(tupleType, offset: 0);
        static TupleConstructorShapeInfo CreateCore(Type tupleType, int offset)
        {
            Debug.Assert(tupleType.IsTupleType());
            ConstructorInfo ctorInfo = tupleType.GetConstructors()[0];
            ParameterInfo[] parameters = ctorInfo.GetParameters();
            MethodParameterShapeInfo[] ctorParameterInfo;
            TupleConstructorShapeInfo? nestedCtor;

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

            return new TupleConstructorShapeInfo(tupleType, ctorInfo, ctorParameterInfo, nestedCtor);

            MethodParameterShapeInfo[] MapParameterInfo(IEnumerable<ParameterInfo> parameters)
                => parameters.Select(p => new MethodParameterShapeInfo(p, logicalName: $"Item{++offset}")).ToArray();
        }
    }
}
