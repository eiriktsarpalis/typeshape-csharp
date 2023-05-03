using System.Collections;

namespace TypeShape.SourceGenerator.Helpers;

public sealed class ImmutableArrayEq<T> : IEquatable<ImmutableArrayEq<T>>, IReadOnlyList<T>
{
#pragma warning disable CA1000 // Do not declare static members on generic types
    public static ImmutableArrayEq<T> Empty { get; } = new ImmutableArrayEq<T>(Array.Empty<T>());
#pragma warning restore CA1000 // Do not declare static members on generic types

    private readonly T[] _values;
    public T this[int index] => _values[index];
    public int Count => _values.Length;

    public ImmutableArrayEq(IEnumerable<T> values)
        => _values = values.ToArray();

    public bool Equals(ImmutableArrayEq<T> other)
        => _values.SequenceEqual(other._values);

    public override bool Equals(object? obj) 
        => obj is ImmutableArrayEq<T> other && Equals(other);

    public override int GetHashCode()
    {
        int hash = 0;
        foreach (T value in _values)
            hash = (hash, value).GetHashCode();
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
        public T Current => _values[_index];
    }
}

public static class ImmutableArrayEq
{
    public static ImmutableArrayEq<T> ToImmutableArrayEq<T>(this IEnumerable<T> values) => new(values);
    public static ImmutableArrayEq<T> Create<T>(params T[] values) => new(values);
}
