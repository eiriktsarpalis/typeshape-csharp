using System.Buffers;
using System.Runtime.InteropServices;

namespace TypeShape.Examples.Utilities;

/// <summary>
/// A simple <see cref="List{T}"/> like implementation that uses pooled arrays.
/// </summary>
public struct PooledList<T> : IDisposable
{
    private T[] _values;
    private int _count;

    /// <summary>Creates an empty <see cref="PooledList{T}"/> instance.</summary>
    public PooledList() : this(4) { }

    /// <summary>Creates an empty <see cref="PooledList{T}"/> instance with specified capacity.</summary>
    public PooledList(int capacity)
    {
        _values = ArrayPool<T>.Shared.Rent(capacity);
        _count = 0;
    }

    /// <summary>Gets the current element count of the list.</summary>
    public readonly int Count { get; }

    /// <summary>Appends a value to the list.</summary>
    public void Add(T value)
    {
        T[] values = _values;

        if (_count == values.Length)
        {
            T[] newValues = ArrayPool<T>.Shared.Rent(values.Length * 2);
            values.CopyTo(newValues, 0);
            ArrayPool<T>.Shared.Return(values, clearArray: true);
            _values = newValues;
        }

        _values[_count++] = value;
    }

    /// <summary>Gets the item contained in the specified index.</summary>
    public readonly ref T this[int index]
    {
        get
        {
            if ((uint)index >= (uint)_count)
            {
                Throw();
                static void Throw() => throw new ArgumentOutOfRangeException(nameof(index));
            }

            return ref _values[index];
        }
    }

    /// <summary>Clears all items from the list.</summary>
    public void Clear()
    {
        _values.AsSpan(0, _count).Clear();
        _count = 0;
    }

    /// <summary>Gets the current list contents as a span.</summary>
    public readonly ReadOnlySpan<T> AsSpan() => _values.AsSpan(0, _count);

    /// <summary>Moves the current context as an array segment, removing the buffer from the list itself.</summary>
    public ArraySegment<T> ExchangeToArraySegment()
    {
        ArraySegment<T> segment = new(_values, 0, _count);
        _values = [];
        _count = 0;
        return segment;
    }

    /// <summary>Disposes of the list, returning any unused buffers to the pool.</summary>
    public void Dispose()
    {
        T[] values = _values;
        values.AsSpan(0, _count).Clear();
        _values = null!;
        _count = 0;
        ArrayPool<T>.Shared.Return(values, clearArray: false);
    }
}
