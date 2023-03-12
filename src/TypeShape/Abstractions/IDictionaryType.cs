namespace TypeShape;

public interface IDictionaryType
{
    IType Type { get; }
    IType KeyType { get; }
    IType ValueType { get; }
    bool IsMutable { get; }
    object? Accept(IDictionaryTypeVisitor visitor, object? state);
}

public interface IDictionaryType<TDictionary, TKey, TValue> : IDictionaryType
    where TKey : notnull
{
    Func<TDictionary, IEnumerable<KeyValuePair<TKey, TValue>>> GetGetEnumerable();
    Setter<TDictionary, KeyValuePair<TKey, TValue>> GetAddKeyValuePair();
}

public interface IDictionaryTypeVisitor
{
    object? VisitDictionaryType<TDictionary, TKey, TValue>(IDictionaryType<TDictionary, TKey, TValue> dictionaryType, object? state) where TKey : notnull;
}
