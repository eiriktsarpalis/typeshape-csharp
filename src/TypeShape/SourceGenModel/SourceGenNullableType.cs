namespace TypeShape.SourceGenModel;

public class SourceGenNullableType<T> : INullableType<T>
    where T : struct
{
    public required IType NullableType { get; init; }
    public required IType ElementType { get; init; }

    public object? Accept(INullableTypeVisitor visitor, object? state)
        => visitor.VisitNullable(this, state);
}
