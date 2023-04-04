namespace TypeShape.SourceGenModel;

public sealed class SourceGenNullableShape<T> : INullableShape<T>
    where T : struct
{
    public required ITypeShape Type { get; init; }
    public required ITypeShape ElementType { get; init; }

    public object? Accept(ITypeShapeVisitor visitor, object? state)
        => visitor.VisitNullable(this, state);
}
