namespace TypeShape.SourceGenModel;

public sealed class SourceGenEnumShape<TEnum, TUnderlying> : IEnumShape<TEnum, TUnderlying>
    where TEnum : struct, Enum
{
    public required ITypeShape Type { get; init; }
    public required ITypeShape UnderlyingType { get; init; }

    public object? Accept(ITypeShapeVisitor visitor, object? state)
        => visitor.VisitEnum(this, state);
}
