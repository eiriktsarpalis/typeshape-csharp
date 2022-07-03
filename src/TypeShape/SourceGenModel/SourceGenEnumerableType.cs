namespace TypeShape.SourceGenModel;

public class SourceGenEnumerableType<TEnumerable, TElement> : IEnumerableType<TEnumerable, TElement>
{
    public required IType<TEnumerable> EnumerableType { get; init; }

    public required IType<TElement> ElementType { get; init; }

    public bool IsMutable => AddDelegateFunc is not null;

    public required Func<TEnumerable, IEnumerable<TElement>> GetEnumerableFunc { get; init; }
    public Setter<TEnumerable, TElement>? AddDelegateFunc { get; init; }

    IType IEnumerableType.EnumerableType => EnumerableType;
    IType IEnumerableType.ElementType => ElementType;

    public object? Accept(IEnumerableTypeVisitor visitor, object? state)
        => visitor.VisitEnumerableType(this, state);

    public Setter<TEnumerable, TElement> GetAddDelegate()
    {
        if (AddDelegateFunc is null)
            throw new InvalidOperationException();

        return AddDelegateFunc;
    }

    public Func<TEnumerable, IEnumerable<TElement>> GetGetEnumerable()
        => GetEnumerableFunc;
}
