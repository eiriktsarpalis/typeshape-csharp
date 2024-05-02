using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using TypeShape.Abstractions;

namespace TypeShape.ReflectionProvider;

[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
internal sealed class ReflectionObjectTypeShape<T>(ReflectionTypeShapeProvider provider) : IObjectTypeShape<T>
{
    ITypeShapeProvider ITypeShape.Provider => provider;

    public TypeShapeKind Kind => TypeShapeKind.Object;

    public bool IsRecord => _isRecord ??= typeof(T).IsRecord();
    private bool? _isRecord;

    public bool IsSimpleType => _isSimpleType ??= DetermineIsSimpleType(typeof(T));
    private bool? _isSimpleType;

    public bool HasProperties => !IsSimpleType && (typeof(T).IsTupleType() || GetMembers().Any());
    public bool HasConstructors => !IsSimpleType && GetConstructors().Any();

    public IEnumerable<IConstructorShape> GetConstructors()
    {
        if (typeof(T).IsAbstract || IsSimpleType)
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

        ConstructorInfo[] constructors = typeof(T).GetConstructors(AllInstanceMembers);
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
        if (IsSimpleType)
        {
            yield break;
        }

        if (typeof(T).IsTupleType())
        {
            foreach (var field in ReflectionHelpers.EnumerateTupleMemberPaths(typeof(T)))
            {
                yield return provider.CreateProperty(typeof(T), field.Member, field.ParentMembers, field.Member, field.LogicalName, includeNonPublicAccessors: false);
            }

            yield break;
        }

        foreach ((MemberInfo memberInfo, ICustomAttributeProvider attributeProvider, string? logicalName, _, bool includeNonPublic) in GetMembers())
        {
            yield return provider.CreateProperty(typeof(T), memberInfo, parentMembers: null, attributeProvider, logicalName, includeNonPublic);
        }
    }

    private IEnumerable<(MemberInfo MemberInfo, ICustomAttributeProvider AttributeProvider, string? LogicalName, int Order, bool IncludeNonPublic)> GetMembers()
    {
        Debug.Assert(!IsSimpleType);
        List<(MemberInfo MemberInfo, ICustomAttributeProvider AttributeProvider, string? LogicalName, int Order, bool IncludeNonPublic)> results = [];
        HashSet<string> membersInScope = new(StringComparer.Ordinal);
        bool isOrderSpecified = false;

        foreach (Type current in typeof(T).GetSortedTypeHierarchy())
        {
            foreach (PropertyInfo propertyInfo in current.GetProperties(AllInstanceMembers))
            {
                if (propertyInfo.GetIndexParameters().Length == 0 &&
                    propertyInfo.PropertyType.CanBeGenericArgument() &&
                    !propertyInfo.IsExplicitInterfaceImplementation() &&
                    !IsOverriddenOrShadowed(propertyInfo))
                {
                    HandleMember(propertyInfo);
                }
            }

            foreach (FieldInfo fieldInfo in current.GetFields(AllInstanceMembers))
            {
                if (fieldInfo.FieldType.CanBeGenericArgument() &&
                    !IsOverriddenOrShadowed(fieldInfo))
                {
                    HandleMember(fieldInfo);
                }
            }
        }

        return isOrderSpecified ? results.OrderBy(r => r.Order) : results;

        bool IsOverriddenOrShadowed(MemberInfo memberInfo) => !membersInScope.Add(memberInfo.Name);

        void HandleMember(MemberInfo memberInfo)
        {
            // Use the most derived member for attribute resolution but
            // use the base definition to determine the member signatures
            // (overrides might declare partial signatures, e.g. only overriding the getter or setter).
            MemberInfo attributeProvider = memberInfo;
            memberInfo = memberInfo is PropertyInfo p ? p.GetBaseDefinition() : memberInfo;

            PropertyShapeAttribute? propertyAttr = attributeProvider.GetCustomAttribute<PropertyShapeAttribute>(inherit: true);
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

            results.Add((memberInfo, attributeProvider, logicalName, order, includeNonPublic));
        }
    }

    private const BindingFlags AllInstanceMembers = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    private static bool DetermineIsSimpleType(Type type)
    {
        // A primitive or self-contained value type that
        // shouldn't expose its properties or constructors.
        return type.IsPrimitive ||
            type == typeof(object) ||
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
            type == typeof(System.Numerics.BigInteger) ||
            typeof(MemberInfo).IsAssignableFrom(type) ||
            typeof(Delegate).IsAssignableFrom(type);
    }
}
