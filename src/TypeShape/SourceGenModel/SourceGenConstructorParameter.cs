namespace TypeShape.SourceGenModel;

public sealed class SourceGenConstructorParameter<TArgumentState, TParameter> : IConstructorParameter<TArgumentState, TParameter>
{
    public required int Position { get; init; }
    public required string? Name { get; init; }
    public required IType<TParameter> ParameterType { get; init; }
    public required Setter<TArgumentState, TParameter> Setter { get; init; }

    public bool HasDefaultValue { get; init; }
    public TParameter? DefaultValue { get; init; }

    object? IConstructorParameter.DefaultValue => DefaultValue;
    IType IConstructorParameter.ParameterType => ParameterType;

    public object? Accept(IConstructorParameterVisitor visitor, object? state)
        => visitor.VisitConstructorParameter(this, state);

    public Setter<TArgumentState, TParameter> GetSetter()
        => Setter;
}
