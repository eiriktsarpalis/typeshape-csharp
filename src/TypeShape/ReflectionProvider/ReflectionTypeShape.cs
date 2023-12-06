using System.Collections;
using System.Collections.Immutable;
using System.Reflection;

namespace TypeShape.ReflectionProvider;

internal sealed class ReflectionTypeShape<T>(ReflectionTypeShapeProvider provider) : ITypeShape<T>
{
    public Type Type => typeof(T);
    public ICustomAttributeProvider AttributeProvider => typeof(T);

    public ITypeShapeProvider Provider => provider;

    public TypeKind Kind => _kind ??= GetTypeKind();
    private TypeKind? _kind;

    public object? Accept(ITypeShapeVisitor visitor, object? state)
        => visitor.VisitType(this, state);

    public IEnumerable<IConstructorShape> GetConstructors(bool nonPublic, bool includeProperties, bool includeFields)
    {
        if (typeof(T).IsAbstract || s_disallowMemberResolution)
        {
            yield break;
        }

        if (typeof(T).IsTupleType())
        {
            IConstructorShapeInfo ctorInfo = ReflectionHelpers.CreateTupleConstructorShapeInfo(typeof(T));
            yield return provider.CreateConstructor(ctorInfo);

            if (typeof(T).IsValueType)
            {
                ctorInfo = CreateDefaultConstructor(memberInitializers: null);
                yield return provider.CreateConstructor(ctorInfo);
            }

            yield break;
        }

        BindingFlags flags = GetInstanceBindingFlags(nonPublic);

        MemberInitializerShapeInfo[] settableMembers = GetMembers(nonPublic, includeFields: true)
            .Where(m => m is PropertyInfo { CanWrite: true } or FieldInfo { IsInitOnly: false })
            .Select(m => new MemberInitializerShapeInfo(m))
            .Where(m => m.IsRequired || (includeFields && m.MemberInfo is FieldInfo) || (includeProperties && m.MemberInfo is PropertyInfo))
            .OrderByDescending(m => m.IsRequired || m.IsInitOnly) // Shift required or init members first
            .ToArray();

        bool isRecord = typeof(T).IsRecord();

        bool isConstructorFound = false;
        foreach (ConstructorInfo constructorInfo in typeof(T).GetConstructors(flags))
        {
            ParameterInfo[] parameters = constructorInfo.GetParameters();
            if (parameters.Any(param => !param.ParameterType.CanBeGenericArgument()))
            {
                // Skip constructors with unsupported parameter types
                continue;
            }

            if (isRecord && parameters is [ParameterInfo parameter] &&
                parameter.ParameterType == typeof(T))
            {
                // Skip the copy constructor in record types
                continue;
            }

            bool setsRequiredMembers = constructorInfo.SetsRequiredMembers();
            bool isDefaultCtorWithoutRequiredOrInitMembers = 
                parameters.Length == 0 && !settableMembers.Any(m => m.IsRequired || m.IsInitOnly);

            var memberInitializers = new List<MemberInitializerShapeInfo>();
            Dictionary<string, Type>? parameterIndex = null;

            foreach (MemberInitializerShapeInfo memberInitializer in isDefaultCtorWithoutRequiredOrInitMembers ? [] : settableMembers)
            {
                if (setsRequiredMembers && memberInitializer.IsRequired)
                {
                    // Skip required members if set by the constructor.
                    continue;
                }

                if (!memberInitializer.IsRequired && memberInitializer.MemberInfo.IsAutoPropertyWithSetter() &&
                    MatchesConstructorParameter(memberInitializer))
                {
                    // Deduplicate any auto-properties whose signature matches a constructor parameter.
                    continue;
                }

                memberInitializers.Add(memberInitializer);

                bool MatchesConstructorParameter(MemberInitializerShapeInfo memberInitializer)
                {
                    parameterIndex ??= parameters.ToDictionary(p => p.Name ?? "", p => p.ParameterType, StringComparer.Ordinal);
                    return parameterIndex.TryGetValue(memberInitializer.Name, out Type? matchingParameterType) &&
                        matchingParameterType == memberInitializer.Type;
                }
            }

            var ctorShapeInfo = new MethodConstructorShapeInfo(typeof(T), constructorInfo, memberInitializers.ToArray());
            yield return provider.CreateConstructor(ctorShapeInfo);
            isConstructorFound = true;
        }

        if (typeof(T).IsValueType && !isConstructorFound)
        {
            // Only emit a default constructor for value types if no explicitly declared constructors were found.
            MethodConstructorShapeInfo ctorShapeInfo = CreateDefaultConstructor(settableMembers);
            yield return provider.CreateConstructor(ctorShapeInfo);
        }
        
        static MethodConstructorShapeInfo CreateDefaultConstructor(MemberInitializerShapeInfo[]? memberInitializers)
            => new(typeof(T), constructorMethod: null, memberInitializers);
    }

    public IEnumerable<IPropertyShape> GetProperties(bool nonPublic, bool includeFields)
    {
        if (typeof(T).IsNestedTupleRepresentation())
        {
            foreach (var field in ReflectionHelpers.EnumerateTupleMemberPaths(typeof(T)))
            {
                yield return provider.CreateProperty(typeof(T), field.Member, field.ParentMembers, logicalName: field.LogicalName, nonPublic: false);
            }

            yield break;
        }

        foreach (MemberInfo memberInfo in GetMembers(nonPublic, includeFields))
        {
            yield return provider.CreateProperty(typeof(T), memberInfo, parentMembers:null, nonPublic);
        }
    }

    private static IEnumerable<MemberInfo> GetMembers(bool nonPublic, bool includeFields)
    {
        if (s_disallowMemberResolution)
        {
            yield break;
        }

        BindingFlags flags = GetInstanceBindingFlags(nonPublic);

        foreach (Type current in typeof(T).GetSortedTypeHierarchy())
        {
            foreach (PropertyInfo propertyInfo in current.GetProperties(flags))
            {
                if (propertyInfo.GetIndexParameters().Length == 0 &&
                    propertyInfo.PropertyType.CanBeGenericArgument() &&
                    !propertyInfo.IsExplicitInterfaceImplementation())
                {
                    yield return propertyInfo;
                }
            }

            if (includeFields)
            {
                foreach (FieldInfo fieldInfo in current.GetFields(flags))
                {
                    if (fieldInfo.FieldType.CanBeGenericArgument())
                    {
                        yield return fieldInfo;
                    }
                }
            }
        }
    }

    public IEnumShape GetEnumShape()
    {
        ValidateKind(TypeKind.Enum);
        return provider.CreateEnumShape(typeof(T));
    }

    public INullableShape GetNullableShape()
    {
        ValidateKind(TypeKind.Nullable);
        return provider.CreateNullableShape(typeof(T));
    }

    public IEnumerableShape GetEnumerableShape()
    {
        ValidateKind(TypeKind.Enumerable);
        return provider.CreateEnumerableShape(typeof(T));
    }

    public IDictionaryShape GetDictionaryShape()
    {
        ValidateKind(TypeKind.Dictionary);
        return provider.CreateDictionaryShape(typeof(T));
    }

    private void ValidateKind(TypeKind expectedKind)
    {
        if ((Kind & expectedKind) == 0)
        {
            throw new InvalidOperationException($"Type {typeof(T)} is not of kind {expectedKind}.");
        }
    }

    private static TypeKind GetTypeKind()
    {
        Type type = typeof(T);

        if (default(T) is null && type.IsValueType)
        {
            return TypeKind.Nullable;
        }

        if (type.IsEnum)
        {
            return TypeKind.Enum;
        }

        if (typeof(IDictionary).IsAssignableFrom(type))
        {
            return TypeKind.Dictionary;
        }
        else
        {
            foreach (Type interfaceTy in type.GetAllInterfaces())
            {
                if (interfaceTy.IsGenericType)
                {
                    Type genericInterfaceTy = interfaceTy.GetGenericTypeDefinition();
                    if (genericInterfaceTy == typeof(IDictionary<,>) ||
                        genericInterfaceTy == typeof(IReadOnlyDictionary<,>))
                    {
                        return TypeKind.Dictionary;
                    }
                }
            }
        }

        if (typeof(IEnumerable).IsAssignableFrom(type) && type != typeof(string))
        {
            return TypeKind.Enumerable;
        }

        return TypeKind.None;
    }

    private static BindingFlags GetInstanceBindingFlags(bool nonPublic)
        => nonPublic 
        ? BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
        : BindingFlags.Public | BindingFlags.Instance;

    private readonly static bool s_disallowMemberResolution = DisallowMemberResolution();
    private static bool DisallowMemberResolution()
    {
        Type type = typeof(T);
        return type.IsPrimitive ||
            type.IsEnum ||
            type.IsArray ||
            type == typeof(object) ||
            type == typeof(string) ||
            type == typeof(decimal) ||
            type == ReflectionHelpers.Int128Type ||
            type == ReflectionHelpers.UInt128Type ||
            type == typeof(DateTime) ||
            type == typeof(DateTimeOffset) ||
            type == typeof(DateOnly) ||
            type == typeof(TimeSpan) ||
            type == typeof(TimeOnly) ||
            type == typeof(Guid) ||
            type == typeof(Version) ||
            type == typeof(Uri) ||
            type == typeof(System.Text.Rune) ||
            ReflectionHelpers.IsNullableStruct<T>() ||
            typeof(MemberInfo).IsAssignableFrom(type) ||
            typeof(Delegate).IsAssignableFrom(type);
    }
}
