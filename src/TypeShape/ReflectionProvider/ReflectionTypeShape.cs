using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace TypeShape.ReflectionProvider;

[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
internal sealed class ReflectionTypeShape<T>(ReflectionTypeShapeProvider provider) : ITypeShape<T>
{
    public ICustomAttributeProvider AttributeProvider => typeof(T);

    public ITypeShapeProvider Provider => provider;

    public TypeKind Kind => _kind ??= GetTypeKind();
    private TypeKind? _kind;

    public bool IsRecord => _isRecord ??= typeof(T).IsRecord();
    private bool? _isRecord;

    public IEnumerable<IConstructorShape> GetConstructors()
    {
        if (typeof(T).IsAbstract || Kind is not TypeKind.Object)
        {
            yield break;
        }

        if (typeof(T).IsTupleType())
        {
            IConstructorShapeInfo ctorInfo = ReflectionTypeShapeProvider.CreateTupleConstructorShapeInfo(typeof(T));
            yield return provider.CreateConstructor(ctorInfo);

            if (typeof(T).IsValueType)
            {
                ctorInfo = CreateDefaultConstructor(memberInitializers: null);
                yield return provider.CreateConstructor(ctorInfo);
            }

            yield break;
        }

        var allMembers = GetMembers().ToArray();
        MemberInitializerShapeInfo[] settableMembers = allMembers
            .Where(m => m.MemberInfo is PropertyInfo { CanWrite: true } or FieldInfo { IsInitOnly: false })
            .Select(m => new MemberInitializerShapeInfo(m.MemberInfo, m.LogicalName))
            .OrderByDescending(m => m.IsRequired || m.IsInitOnly) // Shift required or init members first
            .ToArray();

        ConstructorInfo[] constructors = typeof(T).GetConstructors(AllInstanceMemberBindingFlags);
        bool hasConstructorShapeAttribute = constructors.Any(ctor => ctor.GetCustomAttribute<ConstructorShapeAttribute>() != null);
        bool isConstructorFound = false;

        foreach (ConstructorInfo constructorInfo in constructors)
        {
            if (hasConstructorShapeAttribute)
            {
                // For types that contain the [ConstructorShape] attribute, only consider constructors with the attribute.
                if (constructorInfo.GetCustomAttribute<ConstructorShapeAttribute>() is null)
                {
                    continue;
                }
            }
            else
            {
                // For types that don't contain the [ConstructorShape] attribute, only consider public constructors.
                if (!constructorInfo.IsPublic)
                {
                    continue;
                }
            }

            ParameterInfo[] parameters = constructorInfo.GetParameters();
            if (parameters.Any(param => !param.ParameterType.CanBeGenericArgument()))
            {
                // Skip constructors with unsupported parameter types
                continue;
            }

            if (IsRecord && parameters is [ParameterInfo singleParam] &&
                singleParam.ParameterType == typeof(T))
            {
                // Skip the copy constructor in record types
                continue;
            }

            var parameterShapeInfos = parameters.Select(parameter =>
            {
                ParameterShapeAttribute? parameterAttr = parameter.GetCustomAttribute<ParameterShapeAttribute>();
                string? logicalName = parameterAttr?.Name;
                if (logicalName is null)
                {
                    if (string.IsNullOrEmpty(parameter.Name))
                    {
                        throw new NotSupportedException($"The constructor for type '{parameter.Member.DeclaringType}' has had its parameter names trimmed.");
                    }

                    // If no custom name is specified, attempt to use the custom name from a matching property.
                    logicalName =
                        allMembers.FirstOrDefault(member =>
                            member.LogicalName != null &&
                            member.MemberInfo.GetMemberType() == parameter.ParameterType &&
                            ParameterNameMatchesPropertyName(parameter.Name, member.MemberInfo.Name))
                        .LogicalName;

                    // Match parameter to property names up to camelCase/PascalCase conversion
                    static bool ParameterNameMatchesPropertyName(string parameterName, string propertyName) =>
                        parameterName.Length == propertyName.Length &&
                        char.ToLowerInvariant(parameterName[0]) == char.ToLowerInvariant(propertyName[0]) &&
                        parameterName.AsSpan(start: 1).Equals(propertyName.AsSpan(start: 1), StringComparison.Ordinal);
                }

                return new MethodParameterShapeInfo(parameter, logicalName);
            }).ToArray();

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
                    parameterIndex ??= parameterShapeInfos.ToDictionary(p => p.Name, p => p.ParameterInfo.ParameterType, StringComparer.Ordinal);
                    return parameterIndex.TryGetValue(memberInitializer.Name, out Type? matchingParameterType) &&
                        matchingParameterType == memberInitializer.Type;
                }
            }

            var ctorShapeInfo = new MethodConstructorShapeInfo(typeof(T), constructorInfo, parameterShapeInfos, memberInitializers.ToArray());
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
            => new(typeof(T), constructorMethod: null, parameters: [], memberInitializers: memberInitializers);
    }

    public IEnumerable<IPropertyShape> GetProperties()
    {
        if (Kind is not TypeKind.Object)
        {
            yield break;
        }

        if (typeof(T).IsTupleType())
        {
            foreach (var field in ReflectionHelpers.EnumerateTupleMemberPaths(typeof(T)))
            {
                yield return provider.CreateProperty(typeof(T), field.Member, field.ParentMembers, field.LogicalName, includeNonPublicAccessors: false);
            }

            yield break;
        }

        foreach ((MemberInfo memberInfo, string? logicalName, _, bool includeNonPublic) in GetMembers())
        {
            yield return provider.CreateProperty(typeof(T), memberInfo, parentMembers: null, logicalName, includeNonPublic);
        }
    }

    private IEnumerable<(MemberInfo MemberInfo, string? LogicalName, int Order, bool IncludeNonPublic)> GetMembers()
    {
        Debug.Assert(Kind is TypeKind.Object);

        List<(MemberInfo MemberInfo, string? LogicalName, int Order, bool IncludeNonPublic)> results = [];
        HashSet<string> membersInScope = new(StringComparer.Ordinal);
        bool isOrderSpecified = false;

        foreach (Type current in typeof(T).GetSortedTypeHierarchy())
        {
            foreach (PropertyInfo propertyInfo in current.GetProperties(AllInstanceMemberBindingFlags))
            {
                if (propertyInfo.GetIndexParameters().Length == 0 &&
                    propertyInfo.PropertyType.CanBeGenericArgument() &&
                    !propertyInfo.IsExplicitInterfaceImplementation() &&
                    !IsOverriddenOrShadowed(propertyInfo))
                {
                    HandleMember(propertyInfo);
                }
            }

            foreach (FieldInfo fieldInfo in current.GetFields(AllInstanceMemberBindingFlags))
            {
                if (fieldInfo.FieldType.CanBeGenericArgument() &&
                    !IsOverriddenOrShadowed(fieldInfo))
                {
                    HandleMember(fieldInfo);
                }
            }
        }

        return isOrderSpecified ? results.OrderBy(r => r.Order) : results;

        bool IsOverriddenOrShadowed(MemberInfo memberInfo) =>
            memberInfo.IsOverride() || !membersInScope.Add(memberInfo.Name);

        void HandleMember(MemberInfo memberInfo)
        {
            PropertyShapeAttribute? propertyAttr = memberInfo.GetCustomAttribute<PropertyShapeAttribute>();
            string? logicalName = null;
            bool includeNonPublic = false;
            int order = 0;

            if (propertyAttr != null)
            {
                // If the attribute is present, use the value of the Ignore property to determine its inclusion.
                if (propertyAttr.Ignore)
                {
                    return;
                }

                logicalName = propertyAttr.Name;
                if (propertyAttr.Order != 0)
                {
                    order = propertyAttr.Order;
                    isOrderSpecified = true;
                }

                includeNonPublic = true;
            }
            else
            {
                // If no attribute is present, only include members that have at least one public accessor.
                memberInfo.ResolveAccessibility(out bool isGetterPublic, out bool isSetterPublic);
                if (!isGetterPublic && !isSetterPublic)
                {
                    return;
                }
            }

            results.Add((memberInfo, logicalName, order, includeNonPublic));
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

        if (type.IsMemoryType(out _, out _))
        {
            // Memory<T> or ReadOnlyMemory<T>
            return TypeKind.Enumerable;
        }

        if (IsSimpleValue())
        {
            return TypeKind.None;
        }

        return TypeKind.Object;
    }

    private const BindingFlags AllInstanceMemberBindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    private static bool IsSimpleValue()
    {
        // A primitive or self-contained value type that
        // shouldn't expose its properties or constructors.
        Type type = typeof(T);
        return type.IsPrimitive ||
            type == typeof(string) ||
            type == typeof(decimal) ||
            type == typeof(UInt128) ||
            type == typeof(Int128) ||
            type == typeof(Half) ||
            type == typeof(DateTime) ||
            type == typeof(DateTimeOffset) ||
            type == typeof(DateOnly) ||
            type == typeof(TimeSpan) ||
            type == typeof(TimeOnly) ||
            type == typeof(Guid) ||
            type == typeof(Version) ||
            type == typeof(Uri) ||
            type == typeof(System.Text.Rune) ||
            typeof(MemberInfo).IsAssignableFrom(type) ||
            typeof(Delegate).IsAssignableFrom(type);
    }
}
