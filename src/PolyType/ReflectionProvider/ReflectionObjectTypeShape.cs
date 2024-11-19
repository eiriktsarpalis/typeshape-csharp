using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using PolyType.Abstractions;

namespace PolyType.ReflectionProvider;

[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
internal sealed class ReflectionObjectTypeShape<T>(ReflectionTypeShapeProvider provider) : ReflectionTypeShape<T>(provider), IObjectTypeShape<T>
{
    private static readonly EqualityComparer<(Type Type, string Name)> s_ctorParameterEqualityComparer =
        CommonHelpers.CreateTupleComparer(
            EqualityComparer<Type>.Default,
            CommonHelpers.CamelCaseInvariantComparer.Instance);

    public override TypeShapeKind Kind => TypeShapeKind.Object;
    public override object? Accept(ITypeShapeVisitor visitor, object? state = null) => visitor.VisitObject(this, state);

    public bool IsRecordType => _isRecord ??= typeof(T).IsRecordType();
    private bool? _isRecord;

    public bool IsTupleType => _isTuple ??= typeof(T).IsTupleType();
    private bool? _isTuple;

    public bool IsSimpleType => _isSimpleType ??= DetermineIsSimpleType(typeof(T));
    private bool? _isSimpleType;

    public bool HasProperties => _hasProperties ??= GetProperties().Any();
    private bool? _hasProperties;

    public bool HasConstructor => _hasConstructor ??= GetConstructor() is not null;
    private bool? _hasConstructor;

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
                return Provider.CreateConstructor(ctorInfo);
            }
            else
            {
                IConstructorShapeInfo ctorInfo = ReflectionTypeShapeProvider.CreateTupleConstructorShapeInfo(typeof(T));
                return Provider.CreateConstructor(ctorInfo);
            }
        }

        PropertyShapeInfo[] allMembers;
        MemberInitializerShapeInfo[] settableMembers;
        NullabilityInfoContext? nullabilityCtx = Provider.CreateNullabilityInfoContext();

        (ConstructorInfo Ctor, ParameterInfo[] Parameters, bool HasShapeAttribute)[] ctorCandidates = [..GetCandidateConstructors()];
        if (ctorCandidates.Length == 0)
        {
            if (typeof(T).IsValueType)
            {
                // If no explicit ctor has been defined, use the implicit default constructor for structs.
                allMembers = [.. GetMembers(nullabilityCtx)];
                settableMembers = GetSettableMembers(allMembers, ctorSetsRequiredMembers: false);
                bool hasRequiredOrInitOnlyMembers = settableMembers.Any(m => m.IsRequired || m.IsInitOnly);
                MethodConstructorShapeInfo defaultCtorInfo = CreateDefaultConstructor(hasRequiredOrInitOnlyMembers ? settableMembers : []);
                return Provider.CreateConstructor(defaultCtorInfo);
            }

            return null;
        }

        ConstructorInfo? constructorInfo;
        ParameterInfo[] parameters;
        allMembers = [.. GetMembers(nullabilityCtx)];

        if (ctorCandidates.Length == 1)
        {
            (constructorInfo, parameters, _) = ctorCandidates[0];
        }
        else
        {
            // In case of ambiguity, pick the constructor that maximizes
            // the number of parameters matching read-only members.

            HashSet<(Type, string)> readonlyMembers = allMembers
                .Where(m => m.MemberInfo is PropertyInfo { CanWrite: false } or FieldInfo { IsInitOnly: true })
                .Select(m => (m.MemberInfo.GetMemberType(), m.MemberInfo.Name))
                .ToHashSet(s_ctorParameterEqualityComparer);

            (constructorInfo, parameters, _) = ctorCandidates
                .MaxBy(ctor =>
                {
                    int paramsMatchingReadOnlyMembers = ctor.Parameters.Count(p => readonlyMembers.Contains((p.ParameterType, p.Name!)));

                    // In the event of a tie, favor the ctor with the smallest arity.
                    return (ctor.HasShapeAttribute, paramsMatchingReadOnlyMembers, -ctor.Parameters.Length);
                });
        }

        var parameterShapeInfos = new MethodParameterShapeInfo[parameters.Length];
        int i = 0;

        foreach (ParameterInfo parameter in parameters)
        {
            Debug.Assert(!parameter.IsOut, "must have been filtered earlier");

            Type parameterType = parameter.GetEffectiveParameterType();
            bool isNonNullable = parameter.IsNonNullableAnnotation(nullabilityCtx);
            PropertyShapeInfo? matchingMember = allMembers.FirstOrDefault(member =>
                member.MemberInfo.GetMemberType() == parameterType &&
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
                logicalName = matchingMember?.LogicalName;
            }

            parameterShapeInfos[i++] = new(parameter, isNonNullable, matchingMember?.MemberInfo, logicalName);
        }

        bool setsRequiredMembers = constructorInfo.SetsRequiredMembers();
        settableMembers = GetSettableMembers(allMembers, setsRequiredMembers);
        List<MemberInitializerShapeInfo>? memberInitializers = null;

        if (parameters.Length > 0 || settableMembers.Any(m => m.IsRequired || m.IsInitOnly))
        {
            // Constructors with parameters, or constructors with required or init-only members
            // are deemed to be parameterized, in which case we also include *all* settable
            // members in the shape signature.
            foreach (MemberInitializerShapeInfo memberInitializer in settableMembers)
            {
                if (!memberInitializer.IsRequired && parameterShapeInfos.Any(p => p.MatchingMember == memberInitializer.MemberInfo))
                {
                    // Deduplicate any properties whose signature matches a constructor parameter.
                    continue;
                }

                (memberInitializers ??= []).Add(memberInitializer);
            }
        }

        var ctorShapeInfo = new MethodConstructorShapeInfo(typeof(T), constructorInfo, parameterShapeInfos, memberInitializers?.ToArray());
        return Provider.CreateConstructor(ctorShapeInfo);

        static MethodConstructorShapeInfo CreateDefaultConstructor(MemberInitializerShapeInfo[]? memberInitializers)
            => new(typeof(T), constructorMethod: null, parameters: [], memberInitializers: memberInitializers);

        static MemberInitializerShapeInfo[] GetSettableMembers(PropertyShapeInfo[] allMembers, bool ctorSetsRequiredMembers)
        {
            return allMembers
                .Where(m => m.MemberInfo is PropertyInfo { CanWrite: true } or FieldInfo { IsInitOnly: false })
                .Select(m => new MemberInitializerShapeInfo(m.MemberInfo, m.LogicalName, ctorSetsRequiredMembers, m.IsSetterNonNullable))
                .OrderByDescending(m => m.IsRequired || m.IsInitOnly) // Shift required or init members first
                .ToArray();
        }

        IEnumerable<(ConstructorInfo, ParameterInfo[], bool HasShapeAttribute)> GetCandidateConstructors()
        {
            bool foundCtorWithShapeAttribute = false;
            foreach (ConstructorInfo constructorInfo in typeof(T).GetConstructors(AllInstanceMembers))
            {
                bool hasShapeAttribute = constructorInfo.GetCustomAttribute<ConstructorShapeAttribute>() != null;
                if (hasShapeAttribute)
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
                if (parameters.Any(param => param.IsOut || !param.GetEffectiveParameterType().CanBeGenericArgument()))
                {
                    // Skip constructors with unsupported parameter types or out parameters
                    continue;
                }

                if (IsRecordType && parameters is [ParameterInfo singleParam] &&
                    singleParam.ParameterType == typeof(T))
                {
                    // Skip the copy constructor in record types
                    continue;
                }

                yield return (constructorInfo, parameters, hasShapeAttribute);
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
                PropertyShapeInfo propertyShapeInfo = new(typeof(T), field.Member, field.Member, field.ParentMembers, field.LogicalName);
                yield return Provider.CreateProperty(propertyShapeInfo);
            }

            yield break;
        }

        NullabilityInfoContext? nullabilityCtx = Provider.CreateNullabilityInfoContext();
        foreach (PropertyShapeInfo member in GetMembers(nullabilityCtx))
        {
            yield return Provider.CreateProperty(member);
        }
    }

    private IEnumerable<PropertyShapeInfo> GetMembers(NullabilityInfoContext? nullabilityCtx)
    {
        Debug.Assert(!IsSimpleType);
        List<PropertyShapeInfo> results = [];
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
                    HandleMember(propertyInfo, nullabilityCtx);
                }
            }

            foreach (FieldInfo fieldInfo in current.GetFields(AllInstanceMembers))
            {
                if (fieldInfo.FieldType.CanBeGenericArgument() &&
                    !IsOverriddenOrShadowed(fieldInfo))
                {
                    HandleMember(fieldInfo, nullabilityCtx);
                }
            }
        }

        return isOrderSpecified ? results.OrderBy(r => r.Order) : results;

        bool IsOverriddenOrShadowed(MemberInfo memberInfo) => !membersInScope.Add(memberInfo.Name);

        void HandleMember(MemberInfo memberInfo, NullabilityInfoContext? nullabilityCtx)
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

            memberInfo.ResolveNullableAnnotation(nullabilityCtx, out bool isGetterNonNullable, out bool isSetterNonNullable);
            results.Add(new(
                typeof(T),
                memberInfo,
                attributeProvider,
                LogicalName: logicalName,
                Order: order,
                IncludeNonPublicAccessors: includeNonPublic,
                IsGetterNonNullable: isGetterNonNullable,
                IsSetterNonNullable: isSetterNonNullable));
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
            typeof(Delegate).IsAssignableFrom(type) ||
            typeof(Exception).IsAssignableFrom(type) ||
            typeof(Task).IsAssignableFrom(type);
    }
}
