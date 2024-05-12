using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using TypeShape.Abstractions;

namespace TypeShape.ReflectionProvider;

[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
internal sealed class ReflectionObjectTypeShape<T>(ReflectionTypeShapeProvider provider) : IObjectTypeShape<T>
{
    private static readonly EqualityComparer<(Type Type, string Name)> s_ctorParameterEqualityComparer =
        CommonHelpers.CreateTupleComparer(
            EqualityComparer<Type>.Default,
            CommonHelpers.CamelCaseInvariantComparer.Instance);

    ITypeShapeProvider ITypeShape.Provider => provider;

    public TypeShapeKind Kind => TypeShapeKind.Object;

    public bool IsRecordType => _isRecord ??= typeof(T).IsRecordType();
    private bool? _isRecord;

    public bool IsTupleType => _isTuple ??= typeof(T).IsTupleType();
    private bool? _isTuple;

    public bool IsSimpleType => _isSimpleType ??= DetermineIsSimpleType(typeof(T));
    private bool? _isSimpleType;

    public bool HasProperties => !IsSimpleType && GetMembers().Any();
    public bool HasConstructor => GetConstructor() is not null;

    public IConstructorShape? GetConstructor()
    {
        if (typeof(T).IsAbstract || IsSimpleType)
        {
            return null;
        }

        if (IsTupleType)
        {
            if (typeof(T).IsValueType)
            {
                IConstructorShapeInfo ctorInfo = CreateDefaultConstructor(memberInitializers: null);
                return provider.CreateConstructor(ctorInfo);
            }
            else
            {
                IConstructorShapeInfo ctorInfo = ReflectionTypeShapeProvider.CreateTupleConstructorShapeInfo(typeof(T));
                return provider.CreateConstructor(ctorInfo);
            }
        }

        var allMembers = GetMembers().ToArray();
        MemberInitializerShapeInfo[] settableMembers = allMembers
            .Where(m => m.MemberInfo is PropertyInfo { CanWrite: true } or FieldInfo { IsInitOnly: false })
            .Select(m => new MemberInitializerShapeInfo(m.MemberInfo, m.LogicalName))
            .OrderByDescending(m => m.IsRequired || m.IsInitOnly) // Shift required or init members first
            .ToArray();

        (ConstructorInfo Ctor, ParameterInfo[] Parameters)[] ctorCandidates = [..GetCandidateConstructors()];
        if (ctorCandidates.Length == 0)
        {
            if (typeof(T).IsValueType)
            {
                // If no explicit ctor has been defined, use the implicit default constructor for structs.
                // Do not include member initializers if no required or init-only members are present.
                bool hasRequiredOrInitOnlyMembers = settableMembers.Any(m => m.IsRequired || m.IsInitOnly);
                MethodConstructorShapeInfo defaultCtorInfo = CreateDefaultConstructor(hasRequiredOrInitOnlyMembers ? settableMembers : []);
                return provider.CreateConstructor(defaultCtorInfo);
            }

            return null;
        }

        ConstructorInfo? constructorInfo;
        ParameterInfo[] parameters;

        if (ctorCandidates.Length == 1)
        {
            (constructorInfo, parameters) = ctorCandidates[0];
        }
        else
        {
            // In case of ambiguity, pick the constructor that maximizes
            // the number of parameters matching read-only members.

            HashSet<(Type, string)> readonlyMembers = allMembers
                .Where(m => m.MemberInfo is PropertyInfo { CanWrite: false } or FieldInfo { IsInitOnly: true })
                .Select(m => (m.MemberInfo.GetMemberType(), m.MemberInfo.Name))
                .ToHashSet(s_ctorParameterEqualityComparer);

            (constructorInfo, parameters) = ctorCandidates
                .MaxBy(ctor =>
                {
                    int paramsMatchingReadOnlyMembers = ctor.Parameters.Count(p => readonlyMembers.Contains((p.ParameterType, p.Name!)));

                    // In the event of a tie, favor the ctor with the smallest arity.
                    return (paramsMatchingReadOnlyMembers, -ctor.Parameters.Length);
                });
        }

        var parameterShapeInfos = parameters.Select(parameter =>
        {
            var matchingMember = allMembers.FirstOrDefault(member =>
                member.MemberInfo.GetMemberType() == parameter.ParameterType &&
                CommonHelpers.CamelCaseInvariantComparer.Instance.Equals(parameter.Name, member.MemberInfo.Name));

            ParameterShapeAttribute? parameterAttr = parameter.GetCustomAttribute<ParameterShapeAttribute>();
            string? logicalName = parameterAttr?.Name;
            if (logicalName is null)
            {
                if (string.IsNullOrEmpty(parameter.Name))
                {
                    throw new NotSupportedException($"The constructor for type '{parameter.Member.DeclaringType}' has had its parameter names trimmed.");
                }

                // If no custom name is specified, attempt to use the custom name from a matching property.
                logicalName = matchingMember.LogicalName;
            }

            return new MethodParameterShapeInfo(parameter, matchingMember.MemberInfo, logicalName);
        }).ToArray();

        bool setsRequiredMembers = constructorInfo.SetsRequiredMembers();
        bool isDefaultCtorWithoutRequiredOrInitMembers =
            parameters.Length == 0 && !settableMembers.Any(m => (m.IsRequired && !setsRequiredMembers) || m.IsInitOnly);

        // Do not include member initializers in default constructors that don't have required members or init-only properties.
        var settableMembersToInclude = isDefaultCtorWithoutRequiredOrInitMembers ? [] : settableMembers;

        List<MemberInitializerShapeInfo>? memberInitializers = null;
        foreach (MemberInitializerShapeInfo memberInitializer in settableMembersToInclude)
        {
            if (setsRequiredMembers && memberInitializer.IsRequired)
            {
                // Skip required members if set by the constructor.
                continue;
            }

            if (!memberInitializer.IsRequired && parameterShapeInfos.Any(p => p.MatchingMember == memberInitializer.MemberInfo))
            {
                // Deduplicate any auto-properties whose signature matches a constructor parameter.
                continue;
            }

            (memberInitializers ??= []).Add(memberInitializer);
        }

        var ctorShapeInfo = new MethodConstructorShapeInfo(typeof(T), constructorInfo, parameterShapeInfos, memberInitializers?.ToArray() ?? []);
        return provider.CreateConstructor(ctorShapeInfo);

        static MethodConstructorShapeInfo CreateDefaultConstructor(MemberInitializerShapeInfo[]? memberInitializers)
            => new(typeof(T), constructorMethod: null, parameters: [], memberInitializers: memberInitializers);

        IEnumerable<(ConstructorInfo, ParameterInfo[])> GetCandidateConstructors()
        {
            bool foundCtorWithShapeAttribute = false;
            foreach (ConstructorInfo constructorInfo in typeof(T).GetConstructors(AllInstanceMembers))
            {
                if (constructorInfo.GetCustomAttribute<ConstructorShapeAttribute>() != null)
                {
                    if (foundCtorWithShapeAttribute)
                    {
                        throw new InvalidOperationException(
                            $"The type '{typeof(T)}' has duplicate {nameof(ConstructorShapeAttribute)} annotations.");
                    }

                    foundCtorWithShapeAttribute = true;
                }
                else if (!constructorInfo.IsPublic)
                {
                    // Skip unannotated constructors that aren't public.
                    continue;
                }

                ParameterInfo[] parameters = constructorInfo.GetParameters();
                if (parameters.Any(param => !param.ParameterType.CanBeGenericArgument()))
                {
                    // Skip constructors with unsupported parameter types
                    continue;
                }

                if (IsRecordType && parameters is [ParameterInfo singleParam] &&
                    singleParam.ParameterType == typeof(T))
                {
                    // Skip the copy constructor in record types
                    continue;
                }

                yield return (constructorInfo, parameters);
            }
        }
    }

    public IEnumerable<IPropertyShape> GetProperties()
    {
        if (IsSimpleType)
        {
            yield break;
        }

        if (IsTupleType)
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
