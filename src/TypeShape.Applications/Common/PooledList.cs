using System.Buffers;
using System.Runtime.InteropServices;

namespace TypeShape.Applications;

/// <summary>
/// A simple <see cref="List{T}"/> like implementation that uses pooled arrays.
/// </summary>
public struct PooledList<T> : IDisposable
{
    private T[] _values;
    private int _count;

    public PooledList() : this(4) { }

    public PooledList(int capacity)
    {
        _values = ArrayPool<T>.Shared.Rent(capacity);
        _count = 0;
    }

    public readonly int Count { get; }

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

    public void Clear()
    {
        _values.AsSpan(0, _count).Clear();
        _count = 0;
    }

    public readonly ReadOnlySpan<T> AsSpan() => _values.AsSpan(0, _count);
    public readonly IEnumerable<T> AsEnumerable() => MemoryMarshal.ToEnumerable((ReadOnlyMemory<T>)_values.AsMemory(0, _count));

    public void Dispose()
    {
        T[] values = _values;
        values.AsSpan(0, _count).Clear();
        _values = null!;
        _count = 0;
        ArrayPool<T>.Shared.Return(values, clearArray: false);
    }
}
