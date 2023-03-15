using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace TypeShape.Applications.StructuralEquality;

public static partial class StructuralEqualityComparer
{
    private class DictionaryEqualityComparer<TDictionary, TKey, TValue> : EqualityComparer<TDictionary>
        where TKey : notnull
    {
        public Func<TDictionary, IReadOnlyDictionary<TKey, TValue>>? GetDictionary { get; set; }
        public IEqualityComparer<TKey>? KeyComparer { get; set; }
        public IEqualityComparer<TValue>? ValueComparer { get; set; }

        public override bool Equals(TDictionary? x, TDictionary? y)
        {
            Debug.Assert(GetDictionary != null);
            Debug.Assert(KeyComparer != null);
            Debug.Assert(ValueComparer != null);

            if (x is null || y is null)
            {
                return x is null && y is null;
            }

            IReadOnlyDictionary<TKey, TValue> xDict = GetDictionary(x);
            IReadOnlyDictionary<TKey, TValue> yDict = GetDictionary(y);

            if (xDict.Count != yDict.Count)
            {
                return false;
            }

            return AreEqual(new(xDict, KeyComparer), yDict);
        }

        public override int GetHashCode([DisallowNull] TDictionary obj)
        {
            Debug.Assert(GetDictionary != null);
            Debug.Assert(KeyComparer != null);
            Debug.Assert(ValueComparer != null);

            int hashCode = 0;
            foreach (KeyValuePair<TKey, TValue> kvp in GetDictionary(obj))
            {
                int keyHash = kvp.Key is null ? 0 : KeyComparer.GetHashCode(kvp.Key);
                int valueHash = kvp.Value is null ? 0 : ValueComparer.GetHashCode(kvp.Value);
                hashCode ^= HashCode.Combine(keyHash, valueHash);
            }

            return hashCode;
        }

        protected bool AreEqual(Dictionary<TKey, TValue> dict, IReadOnlyDictionary<TKey, TValue> entries)
        {
            Debug.Assert(KeyComparer == dict.Comparer);
            Debug.Assert(ValueComparer != null);
            Debug.Assert(dict.Count == entries.Count);

            foreach (KeyValuePair<TKey, TValue> entry in entries)
            {
                if (!dict.TryGetValue(entry.Key, out TValue? value) ||
                    !ValueComparer.Equals(entry.Value, value))
                {
                    return false;
                }
            }

            return true;
        }
    }

    private sealed class DictionaryOfKVEqualityComparer<TKey, TValue> : DictionaryEqualityComparer<Dictionary<TKey, TValue>, TKey, TValue>
        where TKey : notnull
    {
        public override bool Equals(Dictionary<TKey, TValue>? x, Dictionary<TKey, TValue>? y)
        {
            Debug.Assert(KeyComparer != null);
            Debug.Assert(ValueComparer != null);

            if (x is null || y is null)
            {
                return x is null && y is null;
            }

            if (x.Count != y.Count)
            {
                return false;
            }

            if (x.Comparer == KeyComparer)
            {
                return AreEqual(x, y);
            }
            else if (y.Comparer == KeyComparer)
            {
                return AreEqual(y, x);
            }

            return AreEqual(new(x, KeyComparer), y);
        }
    }
}
