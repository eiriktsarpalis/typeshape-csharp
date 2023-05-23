using System.Collections;

namespace TypeShape.SourceGenerator.Helpers;

public sealed class ImmutableEquatableArray<T> : IEquatable<ImmutableEquatableArray<T>>, IReadOnlyList<T>
    where T : IEquatable<T>
{
#pragma warning disable CA1000 // Do not declare static members on generic types
    public static ImmutableEquatableArray<T> Empty { get; } = new ImmutableEquatableArray<T>(Array.Empty<T>());
#pragma warning restore CA1000 // Do not declare static members on generic types

    private readonly T[] _values;
    public T this[int index] => _values[index];
    public int Count => _values.Length;

    public ImmutableEquatableArray(IEnumerable<T> values)
        => _values = values.ToArray();

    public bool Equals(ImmutableEquatableArray<T> other)
        => ((ReadOnlySpan<T>)_values).SequenceEqual(other._values);

    public override bool Equals(object? obj) 
        => obj is ImmutableEquatableArray<T> other && Equals(other);

    public override int GetHashCode()
    {
        int hash = 0;
        foreach (T value in _values)
        {
            hash = HashHelpers.Combine(hash, value is null ? 0 : value.GetHashCode());
        }

        return hash;
    }

    public Enumerator GetEnumerator() => new(_values);
    IEnumerator<T> IEnumerable<T>.GetEnumerator() => ((IEnumerable<T>)_values).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<T>)_values).GetEnumerator();

    public struct Enumerator
    {
        private readonly T[] _values;
        private int _index;

        internal Enumerator(T[] values)
        {
            _values = values;
            _index = -1;
        }

        public bool MoveNext() => ++_index < _values.Length;
        public readonly T Current => _values[_index];
    }
}

public static class ImmutableEquatableArray
{
    public static ImmutableEquatableArray<T> Empty<T>() where T : IEquatable<T>
        => ImmutableEquatableArray<T>.Empty;

    public static ImmutableEquatableArray<T> ToImmutableEquatableArray<T>(this IEnumerable<T> values) where T : IEquatable<T>
        => new(values);

    public static ImmutableEquatableArray<T> Create<T>(params T[] values) where T : IEquatable<T>
        => values is null or { Length: 0 } ? ImmutableEquatableArray<T>.Empty : new(values);
}
