using System.Reflection;

namespace TypeShape.SourceGenModel;

public sealed class SourceGenConstructorParameterShape<TArgumentState, TParameter> : IConstructorParameterShape<TArgumentState, TParameter>
{
    public required int Position { get; init; }
    public required string? Name { get; init; }
    public required bool IsRequired { get; init; }
    public required bool IsNonNullable { get; init; }
    public required ITypeShape<TParameter> ParameterType { get; init; }
    public required Setter<TArgumentState, TParameter> Setter { get; init; }
    public Func<ICustomAttributeProvider?>? AttributeProviderFunc { get; init; }
    public ICustomAttributeProvider? AttributeProvider => AttributeProviderFunc?.Invoke();

    public bool HasDefaultValue { get; init; }
    public TParameter? DefaultValue { get; init; }

    object? IConstructorParameterShape.DefaultValue => DefaultValue;
    ITypeShape IConstructorParameterShape.ParameterType => ParameterType;

    public object? Accept(ITypeShapeVisitor visitor, object? state)
        => visitor.VisitConstructorParameter(this, state);

    public Setter<TArgumentState, TParameter> GetSetter()
        => Setter;
}
