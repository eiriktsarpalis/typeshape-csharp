using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace TypeShape.ReflectionProvider;

[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
internal sealed class ReflectionConstructorParameterShape<TArgumentState, TParameter> : IConstructorParameterShape<TArgumentState, TParameter>
{
    private readonly ReflectionTypeShapeProvider _provider;
    private readonly IConstructorShapeInfo _ctorInfo;
    private readonly IParameterShapeInfo _parameterInfo;
    private readonly int _position;

    public ReflectionConstructorParameterShape(
        ReflectionTypeShapeProvider provider,
        IConstructorShapeInfo ctorInfo,
        IParameterShapeInfo parameterInfo,
        int position)
    {
        Debug.Assert(position < ctorInfo.Parameters.Length);

        _ctorInfo = ctorInfo;
        _parameterInfo = parameterInfo;
        _position = position;
        _provider = provider;
    }

    public ITypeShape<TParameter> ParameterType => _provider.GetShape<TParameter>();

    public int Position => _position;
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
        => _provider.MemberAccessor.CreateConstructorArgumentStateSetter<TArgumentState, TParameter>(_ctorInfo, _position);
}

internal interface IParameterShapeInfo
{
    Type Type { get; }
    string Name { get; }
    ConstructorParameterKind Kind { get; }
    ICustomAttributeProvider? AttributeProvider { get; }
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
    public MethodParameterShapeInfo(ParameterInfo parameterInfo, string? logicalName = null)
    {
        Name = logicalName
            ?? parameterInfo.Name
            ?? throw new NotSupportedException($"The constructor for type '{parameterInfo.Member.DeclaringType}' has had its parameter names trimmed.");

        ParameterInfo = parameterInfo;
        IsNonNullable = parameterInfo.IsNonNullableAnnotation();

        if (parameterInfo.TryGetDefaultValueNormalized(out object? defaultValue))
        {
            HasDefaultValue = true;
            DefaultValue = defaultValue;
        }
    }

    public ParameterInfo ParameterInfo { get; }

    public Type Type => ParameterInfo.ParameterType;
    public string Name { get; }
    public ConstructorParameterKind Kind => ConstructorParameterKind.ConstructorParameter;
    public ICustomAttributeProvider? AttributeProvider => ParameterInfo;
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
    public MemberInitializerShapeInfo(MemberInfo memberInfo)
    {
        Debug.Assert(memberInfo is PropertyInfo or FieldInfo);

        Type = memberInfo.MemberType();
        MemberInfo = memberInfo;
        IsRequired = memberInfo.IsRequired();
        IsInitOnly = memberInfo.IsInitOnly();
        IsPublic = memberInfo is FieldInfo { IsPublic: true } or PropertyInfo { GetMethod.IsPublic: true };

        memberInfo.ResolveNullableAnnotation(out _, out bool isSetterNonNullable);
        IsNonNullable = isSetterNonNullable;
    }

    public Type Type { get; }
    public MemberInfo MemberInfo { get; }
    public bool IsRequired { get; }
    public bool IsInitOnly { get; }
    public bool IsNonNullable { get; }
    public bool IsPublic { get; }

    public string Name => MemberInfo.Name;
    public ICustomAttributeProvider? AttributeProvider => MemberInfo;
    public bool HasDefaultValue => false;
    public object? DefaultValue => null;
    public ConstructorParameterKind Kind => 
        MemberInfo.MemberType is MemberTypes.Field 
        ? ConstructorParameterKind.FieldInitializer 
        : ConstructorParameterKind.PropertyInitializer;
}