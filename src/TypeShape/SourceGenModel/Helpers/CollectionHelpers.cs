using System.Collections;
using System.Diagnostics;

namespace TypeShape.SourceGenModel;

public static class CollectionHelpers
{
    public static IReadOnlyDictionary<TKey, TValue> AsReadOnlyDictionary<TDictionary, TKey, TValue>(TDictionary dictionary)
        where TDictionary : IDictionary<TKey, TValue>
        => new ReadOnlyDictionaryWrapper<TDictionary, TKey, TValue>(dictionary);

    public static IReadOnlyDictionary<object, object?> AsReadOnlyDictionary<TDictionary>(TDictionary dictionary)
        where TDictionary : IDictionary
        => new ReadOnlyDictionaryWrapper<TDictionary>(dictionary);

    private sealed class ReadOnlyDictionaryWrapper<TDictionary, TKey, TValue> : IReadOnlyDictionary<TKey, TValue>
        where TDictionary : IDictionary<TKey, TValue>
    {
        private readonly TDictionary _dictionary;
        public ReadOnlyDictionaryWrapper(TDictionary dictionary)
        {
            Debug.Assert(!typeof(TDictionary).IsAssignableTo(typeof(IReadOnlyDictionary<TKey, TValue>)),
                         "types implementing IReadOnlyDictionary should not call the wrapper helper.");
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

    private sealed class ReadOnlyDictionaryWrapper<TDictionary> : IReadOnlyDictionary<object, object?>
        where TDictionary : IDictionary
    {
        private readonly TDictionary _dictionary;
        public ReadOnlyDictionaryWrapper(TDictionary dictionary)
            => _dictionary = dictionary;

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
                yield return new(entry.Key, entry.Value);
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
