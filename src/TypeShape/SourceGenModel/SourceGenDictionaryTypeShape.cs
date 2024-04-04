namespace TypeShape.SourceGenModel;

/// <summary>
/// Source generator model for dictionary shapes.
/// </summary>
/// <typeparam name="TDictionary">The type of the dictionary.</typeparam>
/// <typeparam name="TKey">The type of the dictionary key.</typeparam>
/// <typeparam name="TValue">The type of the dictionary value.</typeparam>
public sealed class SourceGenDictionaryTypeShape<TDictionary, TKey, TValue> : IDictionaryShape<TDictionary, TKey, TValue>
    where TKey : notnull
{
    /// <summary>
    /// The provider that generated this shape.
    /// </summary>
    public required ITypeShapeProvider Provider { get; init; }

    /// <summary>
    /// The type shape of the dictionary key.
    /// </summary>
    public required ITypeShape<TKey> KeyType { get; init; }

    /// <summary>
    /// The type shape of the dictionary value.
    /// </summary>
    public required ITypeShape<TValue> ValueType { get; init; }

    /// <summary>
    /// The function extracts an IDictionary from an instance of the dictionary type.
    /// </summary>
    public required Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> GetDictionaryFunc { get; init; }

    /// <summary>
    /// The construction strategy for the dictionary.
    /// </summary>
    public required CollectionConstructionStrategy ConstructionStrategy { get; init; }

    /// <summary>
    /// The function that constructs a default instance of the dictionary type.
    /// </summary>
    public Func<TDictionary>? DefaultConstructorFunc { get; init; }

    /// <summary>
    /// The function that adds a key-value pair to the dictionary.
    /// </summary>
    public Setter<TDictionary, KeyValuePair<TKey, TValue>>? AddKeyValuePairFunc { get; init; }

    /// <summary>
    /// The function that constructs a dictionary from an enumerable of key-value pairs.
    /// </summary>
    public Func<IEnumerable<KeyValuePair<TKey, TValue>>, TDictionary>? EnumerableConstructorFunc { get; init; }

    /// <summary>
    /// The function that constructs a dictionary from a span of key-value pairs.
    /// </summary>
    public SpanConstructor<KeyValuePair<TKey, TValue>, TDictionary>? SpanConstructorFunc { get; init; }

    Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> IDictionaryShape<TDictionary, TKey, TValue>.GetGetDictionary()
        => GetDictionaryFunc;

    Func<TDictionary> IDictionaryShape<TDictionary, TKey, TValue>.GetDefaultConstructor()
        => DefaultConstructorFunc ?? throw new InvalidOperationException("Dictionary shape does not specify a default constructor.");

    Setter<TDictionary, KeyValuePair<TKey, TValue>> IDictionaryShape<TDictionary, TKey, TValue>.GetAddKeyValuePair()
        => AddKeyValuePairFunc ?? throw new InvalidOperationException("Dictionary shape does not specify an append delegate.");

    Func<IEnumerable<KeyValuePair<TKey, TValue>>, TDictionary> IDictionaryShape<TDictionary, TKey, TValue>.GetEnumerableConstructor()
        => EnumerableConstructorFunc ?? throw new InvalidOperationException("Dictionary shape does not specify an enumerable constructor.");

    SpanConstructor<KeyValuePair<TKey, TValue>, TDictionary> IDictionaryShape<TDictionary, TKey, TValue>.GetSpanConstructor()
        => SpanConstructorFunc ?? throw new InvalidOperationException("Dictionary shape does not specify a span constructor.");
}