namespace TypeShape.SourceGenModel;

public class SourceGenDictionaryType<TDictionary, TKey, TValue> : IDictionaryType<TDictionary, TKey, TValue>
    where TKey : notnull
{
    public required IType<TDictionary> DictionaryType { get; init; }
    public required IType<TKey> KeyType { get; init; }
    public required IType<TValue> ValueType { get; init; }

    public required Func<TDictionary, IEnumerable<KeyValuePair<TKey, TValue>>> GetEnumerableFunc { get; init; }
    public Setter<TDictionary, KeyValuePair<TKey, TValue>>? AddDelegateFunc { get; init; }

    public bool IsMutable => AddDelegateFunc is not null;

    IType IDictionaryType.DictionaryType => DictionaryType;

    IType IDictionaryType.KeyType => KeyType;

    IType IDictionaryType.ValueType => ValueType;

    public object? Accept(IDictionaryTypeVisitor visitor, object? state)
        => visitor.VisitDictionaryType(this, state);

    public Setter<TDictionary, KeyValuePair<TKey, TValue>> GetAddDelegate()
    {
        if (AddDelegateFunc is null)
            throw new InvalidOperationException();

        return AddDelegateFunc;
    }

    public Func<TDictionary, IEnumerable<KeyValuePair<TKey, TValue>>> GetGetEnumerable()
        => GetEnumerableFunc;
}
