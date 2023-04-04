namespace TypeShape.SourceGenModel;

public sealed class SourceGenDictionaryShape<TDictionary, TKey, TValue> : IDictionaryShape<TDictionary, TKey, TValue>
    where TKey : notnull
{
    public required ITypeShape Type { get; init; }
    public required ITypeShape KeyType { get; init; }
    public required ITypeShape ValueType { get; init; }

    public required Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> GetDictionaryFunc { get; init; }
    public Setter<TDictionary, KeyValuePair<TKey, TValue>>? AddKeyValuePairFunc { get; init; }

    public bool IsMutable => AddKeyValuePairFunc is not null;

    public object? Accept(ITypeShapeVisitor visitor, object? state)
        => visitor.VisitDictionary(this, state);

    public Setter<TDictionary, KeyValuePair<TKey, TValue>> GetAddKeyValuePair()
    {
        if (AddKeyValuePairFunc is null)
        {
            throw new InvalidOperationException("Dictionary shape does not specify an append delegate.");
        }

        return AddKeyValuePairFunc;
    }

    public Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> GetGetDictionary()
        => GetDictionaryFunc;
}