namespace TypeShape.SourceGenModel;

public sealed class SourceGenNullableShape<T> : INullableShape<T>
    where T : struct
{
    public required ITypeShape<T> ElementType { get; init; }
}
