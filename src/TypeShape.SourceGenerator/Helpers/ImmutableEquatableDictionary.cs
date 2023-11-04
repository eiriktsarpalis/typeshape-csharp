using System.Collections;
using System.ComponentModel;
using System.Diagnostics;

namespace TypeShape.SourceGenerator.Helpers;

public sealed class ImmutableEquatableDictionary<TKey, TValue> : 
    IEquatable<ImmutableEquatableDictionary<TKey, TValue>>,
    IDictionary<TKey, TValue>, 
    IReadOnlyDictionary<TKey, TValue>, 
    IDictionary

    where TKey : IEquatable<TKey> 
    where TValue : IEquatable<TValue>
{
    public static ImmutableEquatableDictionary<TKey, TValue> Empty { get; } = new([]);

    private readonly Dictionary<TKey, TValue> _values;

    private ImmutableEquatableDictionary(Dictionary<TKey, TValue> values)
    {
        Debug.Assert(values.Comparer == EqualityComparer<TKey>.Default);
        _values = values;
    }

    public int Count => _values.Count;
    public bool ContainsKey(TKey key) => _values.ContainsKey(key);
    public bool TryGetValue(TKey key, out TValue value) => _values.TryGetValue(key, out value);
    public TValue this[TKey key] => _values[key];

    public bool Equals(ImmutableEquatableDictionary<TKey, TValue> other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        Dictionary<TKey, TValue> thisDict = _values;
        Dictionary<TKey, TValue> otherDict = other._values;
        if (thisDict.Count != otherDict.Count)
        {
            return false;
        }

        foreach (KeyValuePair<TKey, TValue> entry in thisDict)
        {
            if (!otherDict.TryGetValue(entry.Key, out TValue? otherValue) ||
                !entry.Value.Equals(otherValue))
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object? obj)
        => obj is ImmutableEquatableDictionary<TKey, TValue> other && Equals(other);

    public override int GetHashCode()
    {
        int hash = 0;
        foreach (KeyValuePair<TKey, TValue> entry in _values)
        {
            int keyHash = entry.Key.GetHashCode();
            int valueHash = entry.Value?.GetHashCode() ?? 0;
            hash ^= HashHelpers.Combine(keyHash, valueHash);
        }

        return hash;
    }

    public Dictionary<TKey, TValue>.Enumerator GetEnumerator() => _values.GetEnumerator();
    public Dictionary<TKey, TValue>.KeyCollection Keys => _values.Keys;
    public Dictionary<TKey, TValue>.ValueCollection Values => _values.Values;

    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static ImmutableEquatableDictionary<TKey, TValue> UnsafeCreateFromDictionary(Dictionary<TKey, TValue> values)
        => new(values);

    #region Explicit interface implementations
    IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator() => _values.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _values.GetEnumerator();
    IDictionaryEnumerator IDictionary.GetEnumerator() => ((IDictionary)_values).GetEnumerator();
    IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => _values.Keys;
    IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => _values.Values;
    ICollection<TKey> IDictionary<TKey, TValue>.Keys => _values.Keys;
    ICollection<TValue> IDictionary<TKey, TValue>.Values => _values.Values;
    ICollection IDictionary.Keys => _values.Keys;
    ICollection IDictionary.Values => _values.Values;

    bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly => true;
    bool IDictionary.IsReadOnly => true;
    bool IDictionary.IsFixedSize => true;
    bool ICollection.IsSynchronized => false;
    object ICollection.SyncRoot => this;

    TValue IDictionary<TKey, TValue>.this[TKey key] { get => _values[key]; set => throw new InvalidOperationException(); }
    object IDictionary.this[object key] { get => ((IDictionary)_values)[key]; set => throw new InvalidOperationException(); }
    bool IDictionary.Contains(object key) => ((IDictionary)_values).Contains(key);
    void ICollection.CopyTo(Array array, int index) => ((IDictionary)_values).CopyTo(array, index);
    bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> item) => _values.Contains(item);
    void ICollection<KeyValuePair<TKey, TValue>>.CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) 
        => ((ICollection<KeyValuePair<TKey,TValue>>)_values).CopyTo(array, arrayIndex);

    void IDictionary<TKey, TValue>.Add(TKey key, TValue value) => throw new InvalidOperationException();
    bool IDictionary<TKey, TValue>.Remove(TKey key) => throw new InvalidOperationException();
    void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> item) => throw new InvalidOperationException();
    bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> item) => throw new InvalidOperationException();
    void ICollection<KeyValuePair<TKey, TValue>>.Clear() => throw new InvalidOperationException();
    void IDictionary.Add(object key, object value) => throw new InvalidOperationException();
    void IDictionary.Remove(object key) => throw new InvalidOperationException();
    void IDictionary.Clear() => throw new InvalidOperationException();
    #endregion
}

public static class ImmutableEquatableDictionary
{
    public static ImmutableEquatableDictionary<TKey, TValue> Empty<TKey, TValue>() 
        where TKey : IEquatable<TKey>
        where TValue : IEquatable<TValue>
        => ImmutableEquatableDictionary<TKey, TValue>.Empty;

    public static ImmutableEquatableDictionary<TKey, TValue> ToImmutableEquatableDictionary<TKey, TValue>(this IEnumerable<TValue> values, Func<TValue, TKey> keySelector) 
        where TKey : IEquatable<TKey>
        where TValue : IEquatable<TValue>
    {
        return values is ICollection { Count: 0 }
            ? ImmutableEquatableDictionary<TKey, TValue>.Empty 
            : ImmutableEquatableDictionary<TKey, TValue>.UnsafeCreateFromDictionary(values.ToDictionary(keySelector));
    }

    public static ImmutableEquatableDictionary<TKey, TValue> ToImmutableEquatableDictionary<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>> values)
        where TKey : IEquatable<TKey>
        where TValue : IEquatable<TValue>
    {
        switch (values)
        {
            case ICollection { Count: 0 }:
                return ImmutableEquatableDictionary<TKey, TValue>.Empty;
            case IDictionary<TKey, TValue> idict:
                return ImmutableEquatableDictionary<TKey, TValue>.UnsafeCreateFromDictionary(new(idict));
            default:
                return ImmutableEquatableDictionary<TKey, TValue>.UnsafeCreateFromDictionary(values.ToDictionary(kvp => kvp.Key, kvp => kvp.Value));
        }
    }
}
