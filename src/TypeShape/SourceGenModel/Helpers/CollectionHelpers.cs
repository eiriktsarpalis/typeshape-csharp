using System.Collections;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace TypeShape.SourceGenModel;

/// <summary>
/// Collection helper methods to be consumed by the source generator.
/// </summary>
public static class CollectionHelpers
{
    /// <summary>
    /// Creates a list from a <see cref="ReadOnlySpan{T}"/>.
    /// </summary>
    /// <typeparam name="T">The element type of the list.</typeparam>
    /// <param name="span">The span containing the elements of the list.</param>
    /// <returns>A new list containing the specified elements.</returns>
    public static List<T> CreateList<T>(ReadOnlySpan<T> span)
    {
        var list = new List<T>(span.Length);
        CollectionsMarshal.SetCount(list, span.Length);
        span.CopyTo(CollectionsMarshal.AsSpan(list));
        return list;
    }

    /// <summary>
    /// Creates a set from a <see cref="ReadOnlySpan{T}"/>.
    /// </summary>
    /// <typeparam name="T">The element type of the set.</typeparam>
    /// <param name="span">The span containing the elements of the set.</param>
    /// <returns>A new set containing the specified elements.</returns>
    public static HashSet<T> CreateHashSet<T>(ReadOnlySpan<T> span)
    {
        var set = new HashSet<T>(span.Length);
        for (int i = 0; i < span.Length; i++)
        {
            set.Add(span[i]); // NB duplicates have overwrite semantics.
        }

        return set;
    }

    /// <summary>
    /// Creates a dictionary from a span of key/value pairs.
    /// </summary>
    /// <typeparam name="TKey">The key type of the dictionary.</typeparam>
    /// <typeparam name="TValue">The value type of the dictionary.</typeparam>
    /// <param name="span">The span containing the entries of the dictionary.</param>
    /// <returns>A new dictionary containing the specified entries.</returns>
    public static Dictionary<TKey, TValue> CreateDictionary<TKey, TValue>(ReadOnlySpan<KeyValuePair<TKey, TValue>> span)
        where TKey : notnull
    {
        var dict = new Dictionary<TKey, TValue>(span.Length);
        for (int i = 0; i < span.Length; i++)
        {
            KeyValuePair<TKey, TValue> kvp = span[i];
            dict[kvp.Key] = kvp.Value; // NB duplicate keys have overwrite semantics.
        }

        return dict;
    }

    /// <summary>
    /// Creates a <see cref="IReadOnlyDictionary{TKey, TValue}"/> adapter for a <see cref="IDictionary{TKey, TValue}"/>.
    /// </summary>
    /// <typeparam name="TDictionary">The dictionary type to be wrapped.</typeparam>
    /// <typeparam name="TKey">The key type of the dictionary.</typeparam>
    /// <typeparam name="TValue">The value type of the dictionary.</typeparam>
    /// <param name="dictionary">The source dictionary to be wrapped.</param>
    /// <returns>A read-only dictionary instance wrapping the source dictionary.</returns>
    public static IReadOnlyDictionary<TKey, TValue> AsReadOnlyDictionary<TDictionary, TKey, TValue>(TDictionary dictionary)
        where TDictionary : IDictionary<TKey, TValue>
        => dictionary is IReadOnlyDictionary<TKey, TValue> rod ? rod : new ReadOnlyDictionaryAdapter<TDictionary, TKey, TValue>(dictionary);

    /// <summary>
    /// Creates a <see cref="IReadOnlyDictionary{TKey, TValue}"/> adapter for a <see cref="IDictionary"/>.
    /// </summary>
    /// <typeparam name="TDictionary">The dictionary type to be wrapped.</typeparam>
    /// <param name="dictionary">The source dictionary to be wrapped.</param>
    /// <returns>A read-only dictionary instance wrapping the source dictionary.</returns>
    public static IReadOnlyDictionary<object, object?> AsReadOnlyDictionary<TDictionary>(TDictionary dictionary)
        where TDictionary : IDictionary
        => dictionary is IReadOnlyDictionary<object, object?> rod ? rod : new ReadOnlyDictionaryAdapter<TDictionary>(dictionary);

    private sealed class ReadOnlyDictionaryAdapter<TDictionary, TKey, TValue> : IReadOnlyDictionary<TKey, TValue>
        where TDictionary : IDictionary<TKey, TValue>
    {
        private readonly TDictionary _dictionary;
        public ReadOnlyDictionaryAdapter(TDictionary dictionary)
        {
            Debug.Assert(dictionary is not IReadOnlyDictionary<TKey, TValue>);
            _dictionary = dictionary;
        }

        public int Count => _dictionary.Count;
        public TValue this[TKey key] => _dictionary[key];
        public IEnumerable<TKey> Keys => _dictionary.Keys;
        public IEnumerable<TValue> Values => _dictionary.Values;
        public bool ContainsKey(TKey key) => _dictionary.ContainsKey(key);
        public bool TryGetValue(TKey key, out TValue value) => _dictionary.TryGetValue(key, out value!);
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => _dictionary.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class ReadOnlyDictionaryAdapter<TDictionary> : IReadOnlyDictionary<object, object?>
        where TDictionary : IDictionary
    {
        private readonly TDictionary _dictionary;

        public ReadOnlyDictionaryAdapter(TDictionary dictionary)
        {
            Debug.Assert(dictionary is not IReadOnlyDictionary<object, object?>);
            _dictionary = dictionary;
        }

        public int Count => _dictionary.Count;
        public IEnumerable<object> Keys => _dictionary.Keys.Cast<object>();
        public IEnumerable<object?> Values => _dictionary.Values.Cast<object?>();
        public object? this[object key] => _dictionary[key];
        public bool ContainsKey(object key) => _dictionary.Contains(key);

        public bool TryGetValue(object key, out object? value)
        {
            if (_dictionary.Contains(key))
            {
                value = _dictionary[key];
                return true;
            }
            else
            {
                value = null;
                return false;
            }
        }

        public IEnumerator<KeyValuePair<object, object?>> GetEnumerator()
        {
            foreach (DictionaryEntry entry in _dictionary)
            {
                yield return new(entry.Key, entry.Value);
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
