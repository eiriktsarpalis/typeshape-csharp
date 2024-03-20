namespace TypeShape.SourceGenModel;

/// <summary>
/// Source generator model for enumerable shapes.
/// </summary>
/// <typeparam name="TEnumerable">The type of the enumerable collection.</typeparam>
/// <typeparam name="TElement">The element type of the collection.</typeparam>
public sealed class SourceGenEnumerableShape<TEnumerable, TElement> : IEnumerableShape<TEnumerable, TElement>
{
    /// <summary>
    /// The shape of the enumerable collection.
    /// </summary>
    public required ITypeShape<TEnumerable> Type { get; init; }

    /// <summary>
    /// The shape of the element type.
    /// </summary>
    public required ITypeShape<TElement> ElementType { get; init; }

    /// <summary>
    /// The rank of the enumerable collection.
    /// </summary>
    public required int Rank { get; init; }

    /// <summary>
    /// The function that retrieves an enumerable from an instance of the collection.
    /// </summary>
    public required Func<TEnumerable, IEnumerable<TElement>> GetEnumerableFunc { get; init; }

    /// <summary>
    /// The construction strategy for the collection.
    /// </summary>
    public required CollectionConstructionStrategy ConstructionStrategy { get; init; }

    /// <summary>
    /// The function that constructs a default instance of the collection.
    /// </summary>
    public Func<TEnumerable>? DefaultConstructorFunc { get; init; }

    /// <summary>
    /// The function that adds an element to the collection.
    /// </summary>
    public Setter<TEnumerable, TElement>? AddElementFunc { get; init; }

    /// <summary>
    /// The function that constructs a collection from an enumerable.
    /// </summary>
    public Func<IEnumerable<TElement>, TEnumerable>? EnumerableConstructorFunc { get; init; }

    /// <summary>
    /// The function that constructs a collection from a span.
    /// </summary>
    public SpanConstructor<TElement, TEnumerable>? SpanConstructorFunc { get; init; }

    Func<TEnumerable, IEnumerable<TElement>> IEnumerableShape<TEnumerable, TElement>.GetGetEnumerable()
        => GetEnumerableFunc;

    Func<TEnumerable> IEnumerableShape<TEnumerable, TElement>.GetDefaultConstructor()
        => DefaultConstructorFunc ?? throw new InvalidOperationException("Enumerable shape does not specify a default constructor.");

    Setter<TEnumerable, TElement> IEnumerableShape<TEnumerable, TElement>.GetAddElement()
        => AddElementFunc ?? throw new InvalidOperationException("Enumerable shape does not specify an append delegate.");

    Func<IEnumerable<TElement>, TEnumerable> IEnumerableShape<TEnumerable, TElement>.GetEnumerableConstructor()
        => EnumerableConstructorFunc ?? throw new InvalidOperationException("Enumerable shape does not specify an enumerable constructor.");

    SpanConstructor<TElement, TEnumerable> IEnumerableShape<TEnumerable, TElement>.GetSpanConstructor()
        => SpanConstructorFunc ?? throw new InvalidOperationException("Enumerable shape does not specify a span constructor.");
}
