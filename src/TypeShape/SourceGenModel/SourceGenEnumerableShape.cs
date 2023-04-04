namespace TypeShape.SourceGenModel;

public sealed class SourceGenEnumerableShape<TEnumerable, TElement> : IEnumerableShape<TEnumerable, TElement>
{
    public required ITypeShape Type { get; init; }
    public required ITypeShape ElementType { get; init; }

    public bool IsMutable => AddElementFunc is not null;

    public required Func<TEnumerable, IEnumerable<TElement>> GetEnumerableFunc { get; init; }
    public Setter<TEnumerable, TElement>? AddElementFunc { get; init; }

    public object? Accept(ITypeShapeVisitor visitor, object? state)
        => visitor.VisitEnumerable(this, state);

    public Setter<TEnumerable, TElement> GetAddElement()
    {
        if (AddElementFunc is null)
            throw new InvalidOperationException("Enumerable shape does not specify an append delegate.");

        return AddElementFunc;
    }

    public Func<TEnumerable, IEnumerable<TElement>> GetGetEnumerable()
        => GetEnumerableFunc;
}
