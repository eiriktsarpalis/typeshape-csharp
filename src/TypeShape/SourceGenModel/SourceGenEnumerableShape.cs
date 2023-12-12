namespace TypeShape.SourceGenModel;

public sealed class SourceGenEnumerableShape<TEnumerable, TElement> : IEnumerableShape<TEnumerable, TElement>
{
    public required ITypeShape<TEnumerable> Type { get; init; }
    public required ITypeShape<TElement> ElementType { get; init; }
    public required int Rank { get; init; }

    public required Func<TEnumerable, IEnumerable<TElement>> GetEnumerableFunc { get; init; }

    public required CollectionConstructionStrategy ConstructionStrategy { get; init; }
    public Func<TEnumerable>? DefaultConstructorFunc { get; init; }
    public Setter<TEnumerable, TElement>? AddElementFunc { get; init; }
    public Func<IEnumerable<TElement>, TEnumerable>? EnumerableConstructorFunc { get; init; }
    public SpanConstructor<TElement, TEnumerable>? SpanConstructorFunc { get; init; }

    public Func<TEnumerable, IEnumerable<TElement>> GetGetEnumerable()
        => GetEnumerableFunc;

    public Func<TEnumerable> GetDefaultConstructor()
        => DefaultConstructorFunc ?? throw new InvalidOperationException("Enumerable shape does not specify a default constructor.");

    public Setter<TEnumerable, TElement> GetAddElement()
        => AddElementFunc ?? throw new InvalidOperationException("Enumerable shape does not specify an append delegate.");

    public Func<IEnumerable<TElement>, TEnumerable> GetEnumerableConstructor()
        => EnumerableConstructorFunc ?? throw new InvalidOperationException("Enumerable shape does not specify an enumerable constructor.");

    public SpanConstructor<TElement, TEnumerable> GetSpanConstructor()
        => SpanConstructorFunc ?? throw new InvalidOperationException("Enumerable shape does not specify a span constructor.");
}
