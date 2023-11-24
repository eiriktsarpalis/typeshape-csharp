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

    public IEnumerable<IConstructorShape> GetConstructors(bool nonPublic)
    {
        if (typeof(T).IsAbstract || s_disallowMemberResolution)
        {
            yield break;
        }

        if (typeof(T).IsNestedTupleRepresentation())
        {
            IConstructorShapeInfo ctorInfo = ReflectionHelpers.CreateNestedTupleConstructorShapeInfo(typeof(T));
            yield return provider.CreateConstructor(ctorInfo);

            if (typeof(T).IsValueType)
            {
                ctorInfo = CreateDefaultConstructor(memberInitializers: null);
                yield return provider.CreateConstructor(ctorInfo);
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

            if (isRecord && parameters is [ParameterInfo parameter] &&
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

            var ctorShapeInfo = new MethodConstructorShapeInfo(typeof(T), constructorInfo, memberInitializers.ToArray());
            yield return provider.CreateConstructor(ctorShapeInfo);
            isDefaultConstructorFound |= parameters.Length == 0;
        }

        if (typeof(T).IsValueType && !isDefaultConstructorFound)
        {
            MethodConstructorShapeInfo ctorShapeInfo = CreateDefaultConstructor(requiredOrInitOnlyMembers);
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
