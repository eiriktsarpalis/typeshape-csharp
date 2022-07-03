namespace TypeShape.SourceGenModel;

public class SourceGenConstructorParameter<TArgumentState, TParameter> : IConstructorParameter<TArgumentState, TParameter>
{
    private TParameter? _defaultValue;
    public required int Position { get; init; }
    public required string? Name { get; init; }
    public required IType<TParameter> ParameterType { get; init; }
    public required Setter<TArgumentState, TParameter> Setter { get; init; }

    public bool HasDefaultValue { get; private init; }
    public TParameter? DefaultValue
    {
        get => _defaultValue;
        init
        {
            _defaultValue = value;
            HasDefaultValue = true;
        }
    }

    object? IConstructorParameter.DefaultValue => _defaultValue;
    IType IConstructorParameter.ParameterType => ParameterType;

    public object? Accept(IConstructorParameterVisitor visitor, object? state)
        => visitor.VisitConstructorParameter(this, state);

    public Setter<TArgumentState, TParameter> GetSetter()
        => Setter;
}
