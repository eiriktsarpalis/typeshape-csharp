namespace TypeShape.SourceGenModel;

public sealed class SourceGenDictionaryType<TDictionary, TKey, TValue> : IDictionaryType<TDictionary, TKey, TValue>
    where TKey : notnull
{
    public required IType Type { get; init; }
    public required IType KeyType { get; init; }
    public required IType ValueType { get; init; }

    public required Func<TDictionary, IEnumerable<KeyValuePair<TKey, TValue>>> GetEnumerableFunc { get; init; }
    public Setter<TDictionary, KeyValuePair<TKey, TValue>>? AddKeyValuePairFunc { get; init; }

    public bool IsMutable => AddKeyValuePairFunc is not null;

    public object? Accept(IDictionaryTypeVisitor visitor, object? state)
        => visitor.VisitDictionaryType(this, state);

    public Setter<TDictionary, KeyValuePair<TKey, TValue>> GetAddKeyValuePair()
    {
        if (AddKeyValuePairFunc is null)
            throw new InvalidOperationException();

        return AddKeyValuePairFunc;
    }

    public Func<TDictionary, IEnumerable<KeyValuePair<TKey, TValue>>> GetGetEnumerable()
        => GetEnumerableFunc;
}
