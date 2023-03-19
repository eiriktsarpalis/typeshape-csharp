using System.Reflection;

namespace TypeShape.ReflectionProvider;

internal sealed class ReflectionConstructorParameter<TArgumentState, TParameter> : IConstructorParameter<TArgumentState, TParameter>
{
    private readonly ReflectionTypeShapeProvider _provider;
    private readonly ConstructorShapeInfo _ctorShapeInfo;
    private readonly object? _defaultValue;

    public ReflectionConstructorParameter(ReflectionTypeShapeProvider provider, ConstructorShapeInfo ctorShapeInfo, ParameterInfo parameterInfo)
    {
        Name = parameterInfo.Name;
        Position = parameterInfo.Position;
        AttributeProvider = parameterInfo;
        IsRequired = true;

        if (parameterInfo.HasDefaultValue)
        {
            _defaultValue = parameterInfo.GetDefaultValueNormalized();
            HasDefaultValue = true;
        }

        _ctorShapeInfo = ctorShapeInfo;
        _provider = provider;
    }

    public ReflectionConstructorParameter(ReflectionTypeShapeProvider provider, ConstructorShapeInfo ctorShapeInfo, MemberInitializerInfo requiredOrInitOnlyMember, int position)
    {
        Name = requiredOrInitOnlyMember.Member.Name;
        AttributeProvider = requiredOrInitOnlyMember.Member;
        IsRequired = requiredOrInitOnlyMember.IsRequired;
        Position = position;

        _ctorShapeInfo = ctorShapeInfo;
        _provider = provider;
    }

    public IType ParameterType => _provider.GetShape<TParameter>();

    public int Position { get; }
    public string? Name { get; }
    public bool HasDefaultValue { get; }
    public bool IsRequired { get; }
    public TParameter? DefaultValue => (TParameter?)_defaultValue;
    object? IConstructorParameter.DefaultValue => _defaultValue;
    public ICustomAttributeProvider? AttributeProvider { get; }

    public Setter<TArgumentState, TParameter> GetSetter()
        => _provider.MemberAccessor.CreateConstructorArgumentStateSetter<TArgumentState, TParameter>(_ctorShapeInfo, Position);

    public object? Accept(IConstructorParameterVisitor visitor, object? state)
        => visitor.VisitConstructorParameter(this, state);
}
