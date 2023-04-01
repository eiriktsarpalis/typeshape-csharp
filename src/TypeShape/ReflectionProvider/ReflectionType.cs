using System.Collections;
using System.Reflection;

namespace TypeShape.ReflectionProvider;

internal sealed class ReflectionType<T> : IType<T>
{
    private readonly ReflectionTypeShapeProvider _provider;
    public ReflectionType(ReflectionTypeShapeProvider provider)
        => _provider = provider;

    public Type Type => typeof(T);
    public ICustomAttributeProvider AttributeProvider => typeof(T);

    public ITypeShapeProvider Provider => _provider;

    public TypeKind Kind => _kind ??= GetTypeKind();
    private TypeKind? _kind;

    public object? Accept(ITypeVisitor visitor, object? state)
        => visitor.VisitType(this, state);

    public IEnumerable<IConstructor> GetConstructors(bool nonPublic)
    {
        if (typeof(T).IsAbstract)
        {
            yield break;
        }

        if (typeof(T).IsNestedTupleRepresentation())
        {
            ConstructorShapeInfo ctorInfo = ReflectionHelpers.CreateNestedTupleConstructorShapeInfo(typeof(T));
            yield return _provider.CreateConstructor(ctorInfo);

            if (typeof(T).IsValueType)
            {
                ConstructorShapeInfo defaultCtorInfo = CreateDefaultConstructor(memberInitializers: null);
                yield return _provider.CreateConstructor(defaultCtorInfo);
            }

            yield break;
        }

        BindingFlags flags = GetInstanceBindingFlags(nonPublic);

        MemberInitializerShapeInfo[] requiredOrInitOnlyMembers = GetMembers(nonPublic, includeFields: true)
            .Select(m => (member: m, isRequired: m.IsRequired(), isInitOnly: m.IsInitOnly()))
            .Where(m => m.isRequired || m.isInitOnly)
            .Select(m => new MemberInitializerShapeInfo(m.member, m.isRequired, m.isInitOnly))
            .ToArray();

        bool isRecord = typeof(T).IsRecord();
        bool isDefaultConstructorFound = false;
        foreach (ConstructorInfo constructorInfo in typeof(T).GetConstructors(flags))
        {
            ParameterInfo[] parameters = constructorInfo.GetParameters();
            if (parameters.Any(param => !param.ParameterType.CanBeGenericArgument()))
            {
                continue;
            }

            ConstructorParameterShapeInfo[] parameterShapes = parameters.Select(p => new ConstructorParameterShapeInfo(p)).ToArray();

            if (isRecord && constructorInfo.GetParameters() is [ParameterInfo parameter] &&
                parameter.ParameterType == typeof(T))
            {
                // Skip the copy constructor in record types
                continue;
            }

            var memberInitializers = new List<MemberInitializerShapeInfo>();
            HashSet<(Type ParameterType, string? Name)>? parameterSet = isRecord ? parameters.Select(p => (p.ParameterType, p.Name)).ToHashSet() : null;
            bool setsRequiredMembers = constructorInfo.SetsRequiredMembers();

            foreach (MemberInitializerShapeInfo memberInitializer in requiredOrInitOnlyMembers)
            {
                if (setsRequiredMembers && memberInitializer.IsRequired)
                {
                    continue;
                }

                // In records, deduplicate any init auto-properties whose signature matches the constructor parameters.
                if (!memberInitializer.IsRequired && memberInitializer.MemberInfo.IsAutoPropertyWithSetter() &&
                    parameterSet?.Contains((memberInitializer.Type, memberInitializer.Name)) == true)
                {
                    continue;
                }

                memberInitializers.Add(memberInitializer);
            }

            var ctorShapeInfo = new ConstructorShapeInfo(typeof(T), constructorInfo, parameterShapes, memberInitializers.ToArray());
            yield return _provider.CreateConstructor(ctorShapeInfo);
            isDefaultConstructorFound |= parameters.Length == 0;
        }

        if (typeof(T).IsValueType && !isDefaultConstructorFound)
        {
            ConstructorShapeInfo ctorShapeInfo = CreateDefaultConstructor(requiredOrInitOnlyMembers);
            yield return _provider.CreateConstructor(ctorShapeInfo);
        }
        
        static ConstructorShapeInfo CreateDefaultConstructor(MemberInitializerShapeInfo[]? memberInitializers)
            => new(typeof(T), constructorInfo: null, Array.Empty<ConstructorParameterShapeInfo>(), memberInitializers);
    }

    public IEnumerable<IProperty> GetProperties(bool nonPublic, bool includeFields)
    {
        if (typeof(T).IsNestedTupleRepresentation())
        {
            foreach (var field in ReflectionHelpers.EnumerateTupleMemberPaths(typeof(T)))
            {
                yield return _provider.CreateProperty(typeof(T), field.Member, field.ParentMembers, logicalName: field.LogicalName, nonPublic: false);
            }

            yield break;
        }

        foreach (MemberInfo memberInfo in GetMembers(nonPublic, includeFields))
        {
            yield return _provider.CreateProperty(typeof(T), memberInfo, parentMembers:null, nonPublic);
        }
    }

    private static IEnumerable<MemberInfo> GetMembers(bool nonPublic, bool includeFields)
    {
        if (s_disallowPropertyResolution)
        {
            yield break;
        }

        BindingFlags flags = GetInstanceBindingFlags(nonPublic);

        foreach (Type current in typeof(T).GetSortedTypeHierarchy())
        {
            foreach (PropertyInfo propertyInfo in current.GetProperties(flags))
            {
                if (propertyInfo.GetIndexParameters().Length == 0 &&
                    propertyInfo.PropertyType.CanBeGenericArgument())
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

    public IEnumerableType GetEnumerableType()
    {
        if ((Kind & TypeKind.Enumerable) == 0)
        {
            throw new InvalidOperationException();
        }

        return _provider.CreateEnumerableType(typeof(T));
    }

    public IDictionaryType GetDictionaryType()
    {
        if ((Kind & TypeKind.Dictionary) == 0)
        {
            throw new InvalidOperationException();
        }

        return _provider.CreateDictionaryType(typeof(T));
    }

    public IEnumType GetEnumType()
    {
        if ((Kind & TypeKind.Enum) == 0)
        {
            throw new InvalidOperationException();
        }

        return _provider.CreateEnumType(typeof(T));
    }

    public INullableType GetNullableType()
    {
        if ((Kind & TypeKind.Nullable) == 0)
        {
            throw new InvalidOperationException();
        }

        return _provider.CreateNullableType(typeof(T));
    }

    private static TypeKind GetTypeKind()
    {
        Type type = typeof(T);
        TypeKind kind = TypeKind.None;

        if (default(T) is null && type.IsValueType)
        {
            return TypeKind.Nullable;
        }

        if (type.IsEnum)
        {
            return TypeKind.Enum;
        }

        if (typeof(IEnumerable).IsAssignableFrom(type) && type != typeof(string))
        {
            kind |= TypeKind.Enumerable;
        }

        if (typeof(IDictionary).IsAssignableFrom(type))
        {
            kind |= TypeKind.Dictionary;
        }
        else
        {
            foreach (Type interfaceTy in type.GetInterfaces())
            {
                if (interfaceTy.IsGenericType)
                {
                    Type genericInterfaceTy = interfaceTy.GetGenericTypeDefinition();
                    if (genericInterfaceTy == typeof(IDictionary<,>) ||
                        genericInterfaceTy == typeof(IReadOnlyDictionary<,>))
                    {
                        kind |= TypeKind.Dictionary;
                        break;
                    }
                }
            }
        }

        return kind;
    }

    private static BindingFlags GetInstanceBindingFlags(bool nonPublic)
        => nonPublic 
        ? BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
        : BindingFlags.Public | BindingFlags.Instance;

    private readonly static bool s_disallowPropertyResolution = DisallowPropertyResolution();
    private static bool DisallowPropertyResolution()
    {
        Type type = typeof(T);
        return type.IsPrimitive ||
            type.IsEnum ||
            type.IsArray ||
            type == typeof(object) ||
            type == typeof(string) ||
            type == typeof(DateTime) ||
            type == typeof(DateTimeOffset) ||
            type == typeof(DateOnly) ||
            type == typeof(TimeSpan) ||
            type == typeof(TimeOnly) ||
            type == typeof(Guid) ||
            type == typeof(decimal) ||
            type == typeof(Version) ||
            type == typeof(Uri) ||
            ReflectionHelpers.IsNullable<T>() ||
            typeof(MemberInfo).IsAssignableFrom(type) ||
            typeof(Delegate).IsAssignableFrom(type);
    }
}
