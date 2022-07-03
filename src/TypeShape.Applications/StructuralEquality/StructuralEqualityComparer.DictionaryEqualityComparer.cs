using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace TypeShape.Applications.StructuralEquality;

public static partial class StructuralEqualityComparer
{
    private sealed class DictionaryEqualityComparer<TDictionary, TKey, TValue> : IEqualityComparer<TDictionary>
        where TKey : notnull
    {
        public Func<TDictionary, IEnumerable<KeyValuePair<TKey, TValue>>>? GetEnumerable { get; set; }
        public IEqualityComparer<TKey>? KeyComparer { get; set; }
        public IEqualityComparer<TValue>? ValueComparer { get; set; }

        public bool Equals(TDictionary? x, TDictionary? y)
        {
            Debug.Assert(GetEnumerable != null);
            Debug.Assert(KeyComparer != null);
            Debug.Assert(ValueComparer != null);

            if (x is null || y is null)
            {
                return x is null && y is null;
            }

            if (typeof(Dictionary<TKey, TValue>).IsAssignableFrom(typeof(TDictionary)))
            {
                Dictionary<TKey, TValue> xDict = (Dictionary<TKey, TValue>)(object)x!;
                Dictionary<TKey, TValue> yDict = (Dictionary<TKey, TValue>)(object)y!;

                if (xDict.Count != yDict.Count)
                {
                    return false;
                }

                if (xDict.Comparer == KeyComparer)
                {
                    return AreEqual(xDict, yDict);
                }
                else if (yDict.Comparer == KeyComparer)
                {
                    return AreEqual(yDict, xDict);
                }

                return AreEqual(new(xDict, KeyComparer), yDict);
            }

            IEnumerable<KeyValuePair<TKey, TValue>> xEnum = GetEnumerable(x);
            IEnumerable<KeyValuePair<TKey, TValue>> yEnum = GetEnumerable(y);

            if (xEnum.TryGetNonEnumeratedCount(out int xCount) &&
                yEnum.TryGetNonEnumeratedCount(out int yCount) &&
                xCount != yCount)
            {
                return false;
            }

            return AreEqual(new(xEnum, KeyComparer), yEnum);
        }

        private bool AreEqual(Dictionary<TKey, TValue> dict, IEnumerable<KeyValuePair<TKey, TValue>> entries)
        {
            Debug.Assert(ValueComparer != null);
            Debug.Assert(dict.Comparer == KeyComparer);

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

        public int GetHashCode([DisallowNull] TDictionary obj)
        {
            Debug.Assert(GetEnumerable != null);
            Debug.Assert(KeyComparer != null);
            Debug.Assert(ValueComparer != null);

            int hashCode = 0;
            foreach (KeyValuePair<TKey, TValue> kvp in GetEnumerable(obj))
            {
                int keyHash = kvp.Key is null ? 0 : KeyComparer.GetHashCode(kvp.Key);
                int valueHash = kvp.Value is null ? 0 : ValueComparer.GetHashCode(kvp.Value);
                hashCode ^= HashCode.Combine(keyHash, valueHash);
            }

            return hashCode;
        }
    }
}
