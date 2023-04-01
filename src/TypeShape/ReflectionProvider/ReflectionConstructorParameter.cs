using System.Diagnostics;
using System.Reflection;

namespace TypeShape.ReflectionProvider;

internal sealed class ReflectionConstructorParameter<TArgumentState, TParameter> : IConstructorParameter<TArgumentState, TParameter>
{
    private readonly ReflectionTypeShapeProvider _provider;
    private readonly ConstructorShapeInfo _ctorInfo;
    private readonly IParameterShapeInfo _parameterInfo;
    private readonly int _position;

    public ReflectionConstructorParameter(
        ReflectionTypeShapeProvider provider, 
        ConstructorShapeInfo ctorInfo, 
        IParameterShapeInfo parameterInfo, 
        int position)
    {
        Debug.Assert(position < ctorInfo.TotalParameters);
        _ctorInfo = ctorInfo;
        _parameterInfo = parameterInfo;
        _position = position;
        _provider = provider;
    }

    public IType ParameterType => _provider.GetShape<TParameter>();

    public int Position => _position;
    public string? Name => _parameterInfo.Name;
    public bool HasDefaultValue => _parameterInfo.HasDefaultValue;
    public bool IsRequired => _parameterInfo.IsRequired;
    public TParameter? DefaultValue => (TParameter?)_parameterInfo.DefaultValue;
    object? IConstructorParameter.DefaultValue => _parameterInfo.DefaultValue;
    public ICustomAttributeProvider? AttributeProvider => _parameterInfo.AttributeProvider;

    public Setter<TArgumentState, TParameter> GetSetter()
        => _provider.MemberAccessor.CreateConstructorArgumentStateSetter<TArgumentState, TParameter>(_ctorInfo, _position);

    public object? Accept(IConstructorParameterVisitor visitor, object? state)
        => visitor.VisitConstructorParameter(this, state);
}

internal interface IParameterShapeInfo
{
    Type Type { get; }
    string? Name { get; }
    ICustomAttributeProvider? AttributeProvider { get; }
    bool IsRequired { get; }
    bool HasDefaultValue { get; }
    object? DefaultValue { get; }
}

internal sealed class ConstructorParameterShapeInfo : IParameterShapeInfo
{
    public ConstructorParameterShapeInfo(ParameterInfo parameterInfo, string? logicalName = null)
    {
        Name = logicalName ?? parameterInfo.Name;
        ParameterInfo = parameterInfo;

        if (parameterInfo.TryGetDefaultValueNormalized(out object? defaultValue))
        {
            HasDefaultValue = true;
            DefaultValue = defaultValue;
        }
    }

    public ParameterInfo ParameterInfo { get; }

    public Type Type => ParameterInfo.ParameterType;
    public string? Name { get; }
    public ICustomAttributeProvider? AttributeProvider => ParameterInfo;
    public bool IsRequired => !ParameterInfo.HasDefaultValue;
    public bool HasDefaultValue { get; }
    public object? DefaultValue { get; }
}

internal sealed class MemberInitializerShapeInfo : IParameterShapeInfo
{
    public MemberInitializerShapeInfo(MemberInfo memberInfo, bool isRequired, bool isInitOnly)
    {
        Debug.Assert(isRequired || isInitOnly);

        Type = memberInfo.MemberType();
        MemberInfo = memberInfo;
        IsRequired = isRequired;
        IsInitOnly = isInitOnly;
    }

    public Type Type { get; }
    public MemberInfo MemberInfo { get; }
    public bool IsRequired { get; }
    public bool IsInitOnly { get; }

    public string Name => MemberInfo.Name;
    public ICustomAttributeProvider? AttributeProvider => MemberInfo;
    public bool HasDefaultValue => false;
    public object? DefaultValue => null;
}