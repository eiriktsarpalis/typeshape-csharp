namespace TypeShape.SourceGenModel;

public sealed class SourceGenDictionaryShape<TDictionary, TKey, TValue> : IDictionaryShape<TDictionary, TKey, TValue>
    where TKey : notnull
{
    public required ITypeShape<TDictionary> Type { get; init; }
    public required ITypeShape<TKey> KeyType { get; init; }
    public required ITypeShape<TValue> ValueType { get; init; }

    public required Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> GetDictionaryFunc { get; init; }

    public required CollectionConstructionStrategy ConstructionStrategy { get; init; }
    public Func<TDictionary>? DefaultConstructorFunc { get; init; }
    public Setter<TDictionary, KeyValuePair<TKey, TValue>>? AddKeyValuePairFunc { get; init; }
    public Func<IEnumerable<KeyValuePair<TKey, TValue>>, TDictionary>? EnumerableConstructorFunc { get; init; }
    public SpanConstructor<KeyValuePair<TKey, TValue>, TDictionary>? SpanConstructorFunc { get; init; }

    public Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> GetGetDictionary()
        => GetDictionaryFunc;

    public Func<TDictionary> GetDefaultConstructor()
        => DefaultConstructorFunc ?? throw new InvalidOperationException("Dictionary shape does not specify a default constructor.");

    public Setter<TDictionary, KeyValuePair<TKey, TValue>> GetAddKeyValuePair()
        => AddKeyValuePairFunc ?? throw new InvalidOperationException("Dictionary shape does not specify an append delegate.");

    public Func<IEnumerable<KeyValuePair<TKey, TValue>>, TDictionary> GetEnumerableConstructor()
        => EnumerableConstructorFunc ?? throw new InvalidOperationException("Dictionary shape does not specify an enumerable constructor.");

    public SpanConstructor<KeyValuePair<TKey, TValue>, TDictionary> GetSpanConstructor()
        => SpanConstructorFunc ?? throw new InvalidOperationException("Dictionary shape does not specify a span constructor.");
}