using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;

namespace TypeShape.ReflectionProvider;

internal static class ReflectionHelpers
{
    public readonly static Type? Int128Type = typeof(int).Assembly.GetType("System.Int128");
    public readonly static Type? UInt128Type = typeof(int).Assembly.GetType("System.UInt128");

    public static bool IsNullableStruct<T>()
    {
        return default(T) is null && typeof(T).IsValueType;
    }

    public static bool IsNullableStruct(this Type type)
    {
        return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);
    }

    public static bool IsNullable(this Type type)
    {
        return !type.IsValueType || type.IsNullableStruct();
    }

    public static bool IsIEnumerable(this Type type)
    {
        return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>);
    }

    public static void ResolveNullableAnnotation(this MemberInfo memberInfo, out bool isGetterNonNullable, out bool isSetterNonNullable)
    {
        if (GetNullabilityInfo(memberInfo) is NullabilityInfo info)
        {
            isGetterNonNullable = info.ReadState is NullabilityState.NotNull;
            isSetterNonNullable = info.WriteState is NullabilityState.NotNull;
        }
        else
        {
            // The member type is a non-nullable struct.
            isGetterNonNullable = true;
            isSetterNonNullable = true;
        }
    }

    public static bool IsNonNullableAnnotation(this ParameterInfo parameterInfo)
    {
        if (GetNullabilityInfo(parameterInfo) is NullabilityInfo info)
        {
            // Workaround for https://github.com/dotnet/runtime/issues/92487
            if (parameterInfo.GetGenericParameterDefinition() is { ParameterType: { IsGenericTypeParameter: true } typeParam })
            {
                // Step 1. Look for nullable annotations on the type parameter.
                if (GetNullableFlags(typeParam) is byte[] flags)
                {
                    return flags[0] == 1;
                }

                // Step 2. Look for nullable annotations on the generic method declaration.
                if (typeParam.DeclaringMethod != null && GetNullableContextFlag(typeParam.DeclaringMethod) is byte flag)
                {
                    return flag == 1;
                }

                // Step 3. Look for nullable annotations on the generic method declaration.
                if (GetNullableContextFlag(typeParam.DeclaringType!) is byte flag2)
                {
                    return flag2 == 1;
                }

                // Default to nullable.
                return false;

                static byte[]? GetNullableFlags(MemberInfo member)
                {
                    Attribute? attr = member.GetCustomAttributes().FirstOrDefault(attr =>
                    {
                        Type attrType = attr.GetType();
                        return attrType.Namespace == "System.Runtime.CompilerServices" && attrType.Name == "NullableAttribute";
                    });

                    return (byte[])attr?.GetType().GetField("NullableFlags")?.GetValue(attr)!;
                }

                static byte? GetNullableContextFlag(MemberInfo member)
                {
                    Attribute? attr = member.GetCustomAttributes().FirstOrDefault(attr =>
                    {
                        Type attrType = attr.GetType();
                        return attrType.Namespace == "System.Runtime.CompilerServices" && attrType.Name == "NullableContextAttribute";
                    });

                    return (byte?)attr?.GetType().GetField("Flag")?.GetValue(attr)!;
                }
            }

            return info.WriteState is NullabilityState.NotNull;
        }
        else
        {
            // The parameter type is a non-nullable struct.
            return true;
        }
    }

    private static NullabilityInfo? GetNullabilityInfo(ICustomAttributeProvider memberInfo)
    {
        Debug.Assert(memberInfo is PropertyInfo or FieldInfo or ParameterInfo);

        switch (memberInfo)
        {
            case PropertyInfo prop when (prop.PropertyType.IsNullable()):
                return new NullabilityInfoContext().Create(prop);

            case FieldInfo field when (field.FieldType.IsNullable()):
                return new NullabilityInfoContext().Create(field);

            case ParameterInfo parameter when (parameter.ParameterType.IsNullable()):
                return new NullabilityInfoContext().Create(parameter);
        }

        return null;
    }

    public static ParameterInfo GetGenericParameterDefinition(this ParameterInfo parameter)
    {
        if (parameter.Member is { DeclaringType.IsConstructedGenericType: true }
                             or MethodInfo { IsConstructedGenericMethod: true })
        {
            var genericMethod = (MethodBase)parameter.Member.GetGenericMemberDefinition()!;
            return genericMethod.GetParameters()[parameter.Position];
        }

        return parameter;
    }

    public static MemberInfo GetGenericMemberDefinition(this MemberInfo member)
    {
        if (member is Type type)
        {
            return type.IsConstructedGenericType ? type.GetGenericTypeDefinition() : type;
        }

        if (member.DeclaringType!.IsConstructedGenericType)
        {
            const BindingFlags AllMemberFlags = 
                BindingFlags.Static | BindingFlags.Instance | 
                BindingFlags.Public | BindingFlags.NonPublic;

            return member.DeclaringType.GetGenericTypeDefinition()
                .GetMember(member.Name, AllMemberFlags)
                .First(m => m.MetadataToken == member.MetadataToken);
        }

        if (member is MethodInfo { IsConstructedGenericMethod: true } method)
        {
            return method.GetGenericMethodDefinition();
        }

        return member;
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

    public static bool IsMemoryType(this Type type, [NotNullWhen(true)] out Type? elementType, out bool isReadOnlyMemory)
    {
        if (type.IsGenericType && type.IsValueType)
        {
            Type genericTypeDefinition = type.GetGenericTypeDefinition();
            if (genericTypeDefinition == typeof(ReadOnlyMemory<>))
            {
                elementType = type.GetGenericArguments()[0];
                isReadOnlyMemory = true;
                return true;
            }
            if (genericTypeDefinition == typeof(Memory<>))
            {
                elementType = type.GetGenericArguments()[0];
                isReadOnlyMemory = false;
                return true;
            }
        }

        elementType = null;
        isReadOnlyMemory = false;
        return false;
    }

    public static bool IsRecord(this Type type)
        => type.GetMethod("<Clone>$", BindingFlags.Public | BindingFlags.Instance) is not null;

    public static bool IsCompilerGenerated(this MemberInfo memberInfo)
        => memberInfo.CustomAttributes.Any(static attr => attr.AttributeType.FullName == "System.Runtime.CompilerServices.CompilerGeneratedAttribute");

    public static bool IsAutoPropertyWithSetter(this MemberInfo memberInfo)
        => memberInfo is PropertyInfo { SetMethod: { } setter } && setter.IsCompilerGenerated();

    public static bool TryGetCollectionBuilderAttribute(this Type type, Type elementType, [NotNullWhen(true)] out MethodInfo? builderMethod)
    {
        builderMethod = null;
        CustomAttributeData? attributeData = type.CustomAttributes.FirstOrDefault(attr => attr.AttributeType.FullName == "System.Runtime.CompilerServices.CollectionBuilderAttribute");

        if (attributeData is null)
        {
            return false;
        }

        Type builderType = (Type)attributeData.ConstructorArguments[0].Value!;
        string methodName = (string)attributeData.ConstructorArguments[1].Value!;

        if (builderType.IsGenericType)
        {
            return false;
        }

        foreach (MethodInfo method in builderType.GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            if (method.Name == methodName && method.GetParameters() is [{ ParameterType: Type parameterType }] &&
                parameterType.IsGenericType && parameterType.GetGenericTypeDefinition() == typeof(ReadOnlySpan<>))
            {
                Type spanElementType = parameterType.GetGenericArguments()[0];
                if (spanElementType == elementType && method.ReturnType == type)
                {
                    builderMethod = method;
                    return true;
                }
                
                if (method.IsGenericMethod && method.GetGenericArguments() is [Type typeParameter] &&
                    spanElementType == typeParameter)
                {
                    MethodInfo specializedMethod = method.MakeGenericMethod(elementType);
                    if (specializedMethod.ReturnType == type)
                    {
                        builderMethod = specializedMethod;
                        // Continue searching since we prioritize non-generic methods.
                    }
                }
            }
        }

        return builderMethod != null;
    }

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
            return !parameterType.IsValueType || parameterType.IsNullableStruct();
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
            return [type];
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
            _ => typeof(ValueTuple<,,,,,,,>).MakeGenericType([.. elementTypes[..7], CreateValueTupleType(elementTypes[7..])]),
        };
    }

    public static IEnumerable<(string LogicalName, MemberInfo Member, MemberInfo[] ParentMembers)> EnumerateTupleMemberPaths(Type tupleType)
    {
        // Walks the nested tuple representation, returning every element field and the parent "Rest" fields needed to access the value.
        Debug.Assert(tupleType.IsTupleType());
        List<MemberInfo> nestedMembers = [];
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

    public static IConstructorShapeInfo CreateTupleConstructorShapeInfo(Type tupleType)
    {
        Debug.Assert(tupleType.IsTupleType());

        if (!tupleType.IsNestedTupleRepresentation())
        {
            // Treat non-nested tuples as regular types.
            ConstructorInfo ctorInfo = tupleType.GetConstructors()[0];
            return new MethodConstructorShapeInfo(tupleType, ctorInfo);
        }

        return CreateNestedTupleCtorInfo(tupleType, offset: 0);

        static TupleConstructorShapeInfo CreateNestedTupleCtorInfo(Type tupleType, int offset)
        {
            Debug.Assert(tupleType.IsTupleType());
            ConstructorInfo ctorInfo = tupleType.GetConstructors()[0];
            ParameterInfo[] parameters = ctorInfo.GetParameters();
            MethodParameterShapeInfo[] ctorParameterInfo;
            TupleConstructorShapeInfo? nestedCtor;

            if (parameters.Length == 8 && parameters[7].ParameterType.IsTupleType())
            {
                ctorParameterInfo = MapParameterInfo(parameters.Take(7));
                nestedCtor = CreateNestedTupleCtorInfo(parameters[7].ParameterType, offset);
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
