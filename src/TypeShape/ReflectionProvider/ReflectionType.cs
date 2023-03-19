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
            yield break;

        BindingFlags flags = GetInstanceBindingFlags(nonPublic);

        MemberInitializerInfo[] requiredOrInitOnlyMembers = GetMembers(nonPublic, includeFields: true)
            .Select(m => (member: m, isRequired: m.IsRequired(), isInitOnly: m.IsInitOnly()))
            .Where(m => m.isRequired || m.isInitOnly)
            .Select(m => new MemberInitializerInfo(m.member, m.isRequired, m.isInitOnly))
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

            if (isRecord && constructorInfo.GetParameters() is [ParameterInfo parameter] &&
                parameter.ParameterType == typeof(T))
            {
                // Skip the copy constructor in record types
                continue;
            }

            var memberInitializers = new List<MemberInitializerInfo>();
            HashSet<(Type ParameterType, string? Name)>? parameterSet = isRecord ? parameters.Select(p => (p.ParameterType, p.Name)).ToHashSet() : null;
            bool setsRequiredMembers = constructorInfo.SetsRequiredMembers();

            foreach (MemberInitializerInfo memberInitializer in requiredOrInitOnlyMembers)
            {
                if (setsRequiredMembers && memberInitializer.IsRequired)
                {
                    continue;
                }

                // In records, deduplicate any init auto-properties whose signature matches the constructor parameters.
                if (!memberInitializer.IsRequired && memberInitializer.Member.IsAutoPropertyWithSetter() &&
                    parameterSet?.Contains((memberInitializer.Type, memberInitializer.Member.Name)) == true)
                {
                    continue;
                }

                memberInitializers.Add(memberInitializer);
            }

            var ctorShapeInfo = new ConstructorShapeInfo(typeof(T), constructorInfo, parameters, memberInitializers.ToArray());
            yield return _provider.CreateConstructor(ctorShapeInfo);
            isDefaultConstructorFound |= parameters.Length == 0;
        }

        if (typeof(T).IsValueType && !isDefaultConstructorFound)
        {
            var ctorShapeInfo = new ConstructorShapeInfo(typeof(T), constructorInfo: null, Array.Empty<ParameterInfo>(), requiredOrInitOnlyMembers);
            yield return _provider.CreateConstructor(ctorShapeInfo);
        }
    }

    public IEnumerable<IProperty> GetProperties(bool nonPublic, bool includeFields)
    {
        foreach (MemberInfo memberInfo in GetMembers(nonPublic, includeFields))
        {
            yield return _provider.CreateProperty(typeof(T), memberInfo, nonPublic);
        }
    }

    private static IEnumerable<MemberInfo> GetMembers(bool nonPublic, bool includeFields)
    {
        // TODO handle interface hierarchies

        BindingFlags flags = GetInstanceBindingFlags(nonPublic);

        foreach (PropertyInfo propertyInfo in typeof(T).GetProperties(flags))
        {
            if (propertyInfo.GetIndexParameters().Length == 0 &&
                propertyInfo.PropertyType.CanBeGenericArgument())
            {
                yield return propertyInfo;
            }
        }

        if (includeFields)
        {
            foreach (FieldInfo fieldInfo in typeof(T).GetFields(flags))
            {
                if (fieldInfo.FieldType.CanBeGenericArgument())
                {
                    yield return fieldInfo;
                }
            }
        }
    }

    public IEnumerableType GetEnumerableType()
        => _provider.CreateEnumerableType(typeof(T));

    public IDictionaryType GetDictionaryType()
        => _provider.CreateDictionaryType(typeof(T));

    public IEnumType GetEnumType()
        => _provider.CreateEnumType(typeof(T));

    public INullableType GetNullableType()
        => _provider.CreateNullableType(typeof(T));

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

        if (typeof(IEnumerable).IsAssignableFrom(type))
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
}
