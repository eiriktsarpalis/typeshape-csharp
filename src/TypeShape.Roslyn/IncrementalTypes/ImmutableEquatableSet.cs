using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace TypeShape.Roslyn;

[DebuggerDisplay("Count = {Count}")]
[DebuggerTypeProxy(typeof(ImmutableEquatableSet<>.DebugView))]
[CollectionBuilder(typeof(ImmutableEquatableSet), nameof(ImmutableEquatableSet.Create))]
public sealed class ImmutableEquatableSet<T> : 
    IEquatable<ImmutableEquatableSet<T>>, 
    ISet<T>,
    IReadOnlyCollection<T>, 
    ICollection

    where T : IEquatable<T>
{
    public static ImmutableEquatableSet<T> Empty { get; } = new([]);

    private readonly HashSet<T> _values;

    private ImmutableEquatableSet(HashSet<T> values)
    {
        Debug.Assert(values.Comparer == EqualityComparer<T>.Default);
        _values = values;
    }

    public int Count => _values.Count;
    public bool Contains(T item)
        => _values.Contains(item);

    public bool Equals(ImmutableEquatableSet<T> other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        HashSet<T> thisSet = _values;
        HashSet<T> otherSet = other._values;
        if (thisSet.Count != otherSet.Count)
        {
            return false;
        }

        foreach (T value in thisSet)
        {
            if (!otherSet.Contains(value))
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object? obj)
        => obj is ImmutableEquatableSet<T> other && Equals(other);

    public override int GetHashCode()
    {
        int hash = 0;
        foreach (T value in _values)
        {
            hash ^= value is null ? 0 : value.GetHashCode();
        }

        return hash;
    }

    public HashSet<T>.Enumerator GetEnumerator() => _values.GetEnumerator();


    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static ImmutableEquatableSet<T> UnsafeCreateFromHashSet(HashSet<T> values)
        => new(values);

    #region Explicit interface implementations
    IEnumerator<T> IEnumerable<T>.GetEnumerator() => _values.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _values.GetEnumerator();

    bool ICollection<T>.IsReadOnly => true;
    void ICollection<T>.CopyTo(T[] array, int arrayIndex) => _values.CopyTo(array, arrayIndex);
    void ICollection.CopyTo(Array array, int index) => ((ICollection)_values).CopyTo(array, index);
    bool ICollection.IsSynchronized => false;
    object ICollection.SyncRoot => this;

    bool ISet<T>.IsSubsetOf(IEnumerable<T> other) => _values.IsSubsetOf(other);
    bool ISet<T>.IsSupersetOf(IEnumerable<T> other) => _values.IsSupersetOf(other);
    bool ISet<T>.IsProperSubsetOf(IEnumerable<T> other) => _values.IsProperSubsetOf(other);
    bool ISet<T>.IsProperSupersetOf(IEnumerable<T> other) => _values.IsProperSupersetOf(other);
    bool ISet<T>.Overlaps(IEnumerable<T> other) => _values.Overlaps(other);
    bool ISet<T>.SetEquals(IEnumerable<T> other) => _values.SetEquals(other);

    void ICollection<T>.Add(T item) => throw new InvalidOperationException();
    bool ISet<T>.Add(T item) => throw new InvalidOperationException();
    void ISet<T>.UnionWith(IEnumerable<T> other) => throw new InvalidOperationException();
    void ISet<T>.IntersectWith(IEnumerable<T> other) => throw new InvalidOperationException();
    void ISet<T>.ExceptWith(IEnumerable<T> other) => throw new InvalidOperationException();
    void ISet<T>.SymmetricExceptWith(IEnumerable<T> other) => throw new InvalidOperationException();
    bool ICollection<T>.Remove(T item) => throw new InvalidOperationException();
    void ICollection<T>.Clear() => throw new InvalidOperationException();
    #endregion

    private sealed class DebugView(ImmutableEquatableSet<T> set)
    {
        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public T[] Items => set.ToArray();
    }
}

public static class ImmutableEquatableSet
{
    public static ImmutableEquatableSet<T> ToImmutableEquatableSet<T>(this IEnumerable<T> values) where T : IEquatable<T>
        => values is ICollection<T> { Count: 0 } ? ImmutableEquatableSet<T>.Empty : ImmutableEquatableSet<T>.UnsafeCreateFromHashSet(new(values));

    public static ImmutableEquatableSet<T> Create<T>(ReadOnlySpan<T> values) where T : IEquatable<T>
    {
        if (values.IsEmpty)
        {
            return ImmutableEquatableSet<T>.Empty;
        }

        HashSet<T> hashSet = new();
        for (int i = 0; i < values.Length; i++)
        {
            hashSet.Add(values[i]);
        }
        return ImmutableEquatableSet<T>.UnsafeCreateFromHashSet(hashSet);
    }
}
