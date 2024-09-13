using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using TypeShape.Abstractions;

namespace TypeShape.ReflectionProvider;

[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
internal sealed class ReflectionConstructorParameterShape<TArgumentState, TParameter> : IConstructorParameterShape<TArgumentState, TParameter>
{
    private readonly ReflectionTypeShapeProvider _provider;
    private readonly IConstructorShapeInfo _ctorInfo;
    private readonly IParameterShapeInfo _parameterInfo;

    public ReflectionConstructorParameterShape(
        ReflectionTypeShapeProvider provider,
        IConstructorShapeInfo ctorInfo,
        IParameterShapeInfo parameterInfo,
        int position)
    {
        Debug.Assert(position < ctorInfo.Parameters.Length);

        _ctorInfo = ctorInfo;
        _parameterInfo = parameterInfo;
        Position = position;
        _provider = provider;
    }

    public ITypeShape<TParameter> ParameterType => _provider.GetShape<TParameter>();

    public int Position { get; }
    public string Name => _parameterInfo.Name;
    public ConstructorParameterKind Kind => _parameterInfo.Kind;
    public bool HasDefaultValue => _parameterInfo.HasDefaultValue;
    public bool IsRequired => _parameterInfo.IsRequired;
    public bool IsNonNullable => _parameterInfo.IsNonNullable;
    public bool IsPublic => _parameterInfo.IsPublic;
    public TParameter? DefaultValue => (TParameter?)_parameterInfo.DefaultValue;
    object? IConstructorParameterShape.DefaultValue => _parameterInfo.DefaultValue;
    public ICustomAttributeProvider? AttributeProvider => _parameterInfo.AttributeProvider;

    public Setter<TArgumentState, TParameter> GetSetter()
        => _provider.MemberAccessor.CreateConstructorArgumentStateSetter<TArgumentState, TParameter>(_ctorInfo, Position);
}

internal interface IParameterShapeInfo
{
    Type Type { get; }
    string Name { get; }
    ConstructorParameterKind Kind { get; }
    ICustomAttributeProvider? AttributeProvider { get; }
    bool IsByRef { get; }
    bool IsRequired { get; }
    bool IsNonNullable { get; }
    bool IsPublic { get; }
    bool HasDefaultValue { get; }
    object? DefaultValue { get; }
}

[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
internal sealed class MethodParameterShapeInfo : IParameterShapeInfo
{
    public MethodParameterShapeInfo(ParameterInfo parameterInfo, bool isNonNullable, MemberInfo? matchingMember = null, string? logicalName = null)
    {
        string? name = logicalName ?? parameterInfo.Name;
        Debug.Assert(name != null);
        Name = name;

        Type = parameterInfo.GetEffectiveParameterType();
        ParameterInfo = parameterInfo;
        MatchingMember = matchingMember;
        IsNonNullable = isNonNullable;

        if (parameterInfo.TryGetDefaultValueNormalized(out object? defaultValue))
        {
            HasDefaultValue = true;
            DefaultValue = defaultValue;
        }
    }

    public ParameterInfo ParameterInfo { get; }
    public MemberInfo? MatchingMember { get; }

    public Type Type { get; }
    public string Name { get; }
    public ConstructorParameterKind Kind => ConstructorParameterKind.ConstructorParameter;
    public ICustomAttributeProvider? AttributeProvider => ParameterInfo;
    public bool IsByRef => ParameterInfo.ParameterType.IsByRef;
    public bool IsRequired => !ParameterInfo.HasDefaultValue;
    public bool IsNonNullable { get; }
    public bool IsPublic => true;
    public bool HasDefaultValue { get; }
    public object? DefaultValue { get; }
}

[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
internal sealed class MemberInitializerShapeInfo : IParameterShapeInfo
{
    public MemberInitializerShapeInfo(MemberInfo memberInfo, string? logicalName, bool ctorSetsRequiredMembers, bool isSetterNonNullable)
    {
        Debug.Assert(memberInfo is PropertyInfo or FieldInfo);

        Type = memberInfo.MemberType();
        Name = logicalName ?? memberInfo.Name;
        MemberInfo = memberInfo;
        IsRequired = !ctorSetsRequiredMembers && memberInfo.IsRequired();
        IsInitOnly = memberInfo.IsInitOnly();
        IsPublic = memberInfo is FieldInfo { IsPublic: true } or PropertyInfo { GetMethod.IsPublic: true };
        IsNonNullable = isSetterNonNullable;
    }

    public Type Type { get; }
    public MemberInfo MemberInfo { get; }
    public bool IsByRef => false;
    public bool IsRequired { get; }
    public bool IsInitOnly { get; }
    public bool IsNonNullable { get; }
    public bool IsPublic { get; }

    public string Name { get; }
    public ICustomAttributeProvider? AttributeProvider => MemberInfo;
    public bool HasDefaultValue => false;
    public object? DefaultValue => null;
    public ConstructorParameterKind Kind =>
        MemberInfo.MemberType is MemberTypes.Field
        ? ConstructorParameterKind.FieldInitializer
        : ConstructorParameterKind.PropertyInitializer;
}