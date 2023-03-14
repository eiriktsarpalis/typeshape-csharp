using System.Collections;

namespace TypeShape.ReflectionProvider;

internal sealed class ReflectionGenericDictionaryType<TDictionary, TKey, TValue> : IDictionaryType<TDictionary, TKey, TValue>
    where TDictionary : IDictionary<TKey, TValue>
    where TKey : notnull
{
    private readonly ReflectionTypeShapeProvider _provider;
    
    public ReflectionGenericDictionaryType(ReflectionTypeShapeProvider provider)
        => _provider = provider;

    public IType Type => _provider.GetShape(typeof(TDictionary));

    public IType KeyType => _provider.GetShape(typeof(TKey));

    public IType ValueType => _provider.GetShape(typeof(TValue));

    public bool IsMutable => true;

    public object? Accept(IDictionaryTypeVisitor visitor, object? state)
        => visitor.VisitDictionaryType(this, state);

    public Setter<TDictionary, KeyValuePair<TKey, TValue>> GetAddKeyValuePair()
    {
        return static (ref TDictionary dict, KeyValuePair<TKey, TValue> kvp) => dict[kvp.Key] = kvp.Value;
    }

    public Func<TDictionary, IEnumerable<KeyValuePair<TKey, TValue>>> GetGetEnumerable()
    {
        return static dict => dict;
    }
}

internal sealed class ReflectionReadOnlyDictionaryType<TDictionary, TKey, TValue> : IDictionaryType<TDictionary, TKey, TValue>
    where TDictionary : IReadOnlyDictionary<TKey, TValue>
    where TKey : notnull
{
    private readonly ReflectionTypeShapeProvider _provider;

    public ReflectionReadOnlyDictionaryType(ReflectionTypeShapeProvider provider)
        => _provider = provider;

    public IType Type => _provider.GetShape(typeof(TDictionary));

    public IType KeyType => _provider.GetShape(typeof(TKey));

    public IType ValueType => _provider.GetShape(typeof(TValue));

    public bool IsMutable => false;

    public object? Accept(IDictionaryTypeVisitor visitor, object? state)
        => visitor.VisitDictionaryType(this, state);

    public Setter<TDictionary, KeyValuePair<TKey, TValue>> GetAddKeyValuePair()
    {
        throw new NotSupportedException();
    }

    public Func<TDictionary, IEnumerable<KeyValuePair<TKey, TValue>>> GetGetEnumerable()
    {
        return static dict => dict;
    }
}

internal sealed class ReflectionDictionaryType<TDictionary> : IDictionaryType<TDictionary, object, object?>
    where TDictionary : IDictionary
{
    private readonly ReflectionTypeShapeProvider _provider;

    public ReflectionDictionaryType(ReflectionTypeShapeProvider provider)
        => _provider = provider;

    public IType Type => _provider.GetShape(typeof(TDictionary));

    public IType KeyType => _provider.GetShape(typeof(object));

    public IType ValueType => _provider.GetShape(typeof(object));

    public bool IsMutable => true;

    public object? Accept(IDictionaryTypeVisitor visitor, object? state)
        => visitor.VisitDictionaryType(this, state);

    public Setter<TDictionary, KeyValuePair<object, object?>> GetAddKeyValuePair()
    {
        return static (ref TDictionary dict, KeyValuePair<object, object?> kvp) => dict[kvp.Key] = kvp.Value;
    }

    public Func<TDictionary, IEnumerable<KeyValuePair<object, object?>>> GetGetEnumerable()
    {
        return GetEnumerable;
        static IEnumerable<KeyValuePair<object, object?>> GetEnumerable(TDictionary dict)
        {
            foreach (DictionaryEntry entry in dict)
            {
                yield return new(entry.Key, entry.Value);
            }
        }
    }
}