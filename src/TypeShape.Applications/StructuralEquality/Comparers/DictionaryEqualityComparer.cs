using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace TypeShape.Applications.StructuralEquality.Comparers;

internal abstract class DictionaryEqualityComparerBase<TDictionary, TKey, TValue> : EqualityComparer<TDictionary>
    where TKey : notnull
{
    public required IEqualityComparer<TKey> KeyComparer { get; init; }
    public required IEqualityComparer<TValue> ValueComparer { get; init; }

    private protected bool AreEqual(Dictionary<TKey, TValue> dict, IReadOnlyDictionary<TKey, TValue> entries)
    {
        Debug.Assert(KeyComparer == dict.Comparer);
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

internal sealed class DictionaryEqualityComparer<TDictionary, TKey, TValue> : DictionaryEqualityComparerBase<TDictionary, TKey, TValue>
    where TKey : notnull
{
    public required Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> GetDictionary { get; init; }

    public override bool Equals(TDictionary? x, TDictionary? y)
    {
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
        int hashCode = 0;
        foreach (KeyValuePair<TKey, TValue> kvp in GetDictionary(obj))
        {
            int keyHash = kvp.Key is null ? 0 : KeyComparer.GetHashCode(kvp.Key);
            int valueHash = kvp.Value is null ? 0 : ValueComparer.GetHashCode(kvp.Value);
            hashCode ^= HashCode.Combine(keyHash, valueHash);
        }

        return hashCode;
    }
}

internal sealed class DictionaryOfKVEqualityComparer<TKey, TValue> : DictionaryEqualityComparerBase<Dictionary<TKey, TValue>, TKey, TValue>
    where TKey : notnull
{
    public override bool Equals(Dictionary<TKey, TValue>? x, Dictionary<TKey, TValue>? y)
    {
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

    public override int GetHashCode([DisallowNull] Dictionary<TKey, TValue> obj)
    {
        int hashCode = 0;
        foreach (KeyValuePair<TKey, TValue> kvp in obj)
        {
            int keyHash = kvp.Key is null ? 0 : KeyComparer.GetHashCode(kvp.Key);
            int valueHash = kvp.Value is null ? 0 : ValueComparer.GetHashCode(kvp.Value);
            hashCode ^= HashCode.Combine(keyHash, valueHash);
        }

        return hashCode;
    }
}