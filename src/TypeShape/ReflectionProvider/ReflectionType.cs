using System.Collections;
using System.Reflection;
using TypeShape.Abstractions;

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

        bool isDefaultConstructorFound = false;
        foreach (ConstructorInfo constructorInfo in typeof(T).GetConstructors(flags))
        {
            ParameterInfo[] parameters = constructorInfo.GetParameters();
            if (parameters.Any(param => !param.ParameterType.CanBeGenericArgument()))
            {
                continue;
            }

            yield return _provider.CreateConstructor(typeof(T), constructorInfo, parameters);
            isDefaultConstructorFound |= parameters.Length == 0;
        }

        if (typeof(T).IsValueType && !isDefaultConstructorFound)
        {
            yield return _provider.CreateConstructor(typeof(T), constructorInfo: null, Array.Empty<ParameterInfo>());
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
