namespace TypeShape.SourceGenModel;

public sealed class SourceGenNullableType<T> : INullableType<T>
    where T : struct
{
    public required IType Type { get; init; }
    public required IType ElementType { get; init; }

    public object? Accept(INullableTypeVisitor visitor, object? state)
        => visitor.VisitNullable(this, state);
}
