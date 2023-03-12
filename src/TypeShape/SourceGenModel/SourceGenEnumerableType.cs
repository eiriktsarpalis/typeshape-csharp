namespace TypeShape.SourceGenModel;

public sealed class SourceGenEnumerableType<TEnumerable, TElement> : IEnumerableType<TEnumerable, TElement>
{
    public required IType Type { get; init; }
    public required IType ElementType { get; init; }

    public bool IsMutable => AddElementFunc is not null;

    public required Func<TEnumerable, IEnumerable<TElement>> GetEnumerableFunc { get; init; }
    public Setter<TEnumerable, TElement>? AddElementFunc { get; init; }

    public object? Accept(IEnumerableTypeVisitor visitor, object? state)
        => visitor.VisitEnumerableType(this, state);

    public Setter<TEnumerable, TElement> GetAddElement()
    {
        if (AddElementFunc is null)
            throw new InvalidOperationException();

        return AddElementFunc;
    }

    public Func<TEnumerable, IEnumerable<TElement>> GetGetEnumerable()
        => GetEnumerableFunc;
}
