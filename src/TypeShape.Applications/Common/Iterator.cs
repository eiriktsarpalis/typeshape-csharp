using System.Collections;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace TypeShape.Applications.Common;

/// <summary>
/// Defines a simplified Java Stream like abstraction that allows iterating over collections 
/// without allocating enumerators. Cf. https://arxiv.org/abs/1406.6631 for more details.
/// </summary>
/// <typeparam name="TIterable">The collection type to iterate over.</typeparam>
/// <typeparam name="TElement">The element type of the collection.</typeparam>
public interface IIterator<in TIterable, TElement>
{
    /// <summary>
    /// Iterates all values in a collection using the specified consumer delegate.
    /// </summary>
    void Iterate<TState>(TIterable iterable, Consumer<TElement, TState> iteration, ref TState state);
}

/// <summary>
/// Delegate used for iterating over a single element in a collection.
/// </summary>
public delegate void Consumer<TElement, TState>(TElement element, ref TState state);

public static class Iterator
{
    /// <summary>
    /// Creates an iterator instance from the specified enumerable shape.
    /// </summary>
    public static IIterator<TEnumerable, TElement> Create<TEnumerable, TElement>(IEnumerableTypeShape<TEnumerable, TElement> typeShape)
    {
        if (typeof(TEnumerable).IsArray)
        {
            return typeof(TEnumerable).GetArrayRank() switch
            {
                1 => (IIterator<TEnumerable, TElement>)(object)new ArrayIterator<TElement>(),
                2 => (IIterator<TEnumerable, TElement>)(object)new Array2DIterator<TElement>(),
                _ => (IIterator<TEnumerable, TElement>)(object)new MultiDimensionalArrayIterator<TElement>(),
            };
        }

        if (typeof(TEnumerable) == typeof(ImmutableArray<TElement>))
        {
            return (IIterator<TEnumerable, TElement>)(object)new ImmutableArrayIterator<TElement>();
        }

        if (typeof(TEnumerable) == typeof(Memory<TElement>))
        {
            return (IIterator<TEnumerable, TElement>)(object)new MemoryOfTIterator<TElement>();
        }

        if (typeof(TEnumerable) == typeof(ReadOnlyMemory<TElement>))
        {
            return (IIterator<TEnumerable, TElement>)(object)new ReadOnlyMemoryOfTIterator<TElement>();
        }

        if (typeof(List<TElement>).IsAssignableFrom(typeof(TEnumerable)))
        {
            return (IIterator<TEnumerable, TElement>)(object)new ListIterator<TElement>();
        }
        
        if (!typeof(TEnumerable).IsValueType)
        {
            // For value types we can't rely on contravariance so avoid using them on interface iterators.
            if (typeof(IList<TElement>).IsAssignableFrom(typeof(TEnumerable)))
            {
                return (IIterator<TEnumerable, TElement>)(object)new IListOfTIterator<TElement>();
            }

            if (typeof(IList).IsAssignableFrom(typeof(TEnumerable)))
            {
                Debug.Assert(typeof(TElement) == typeof(object));
                return (IIterator<TEnumerable, TElement>)(object)new IListIterator();
            }
        }

        // Default to an iterator that just allocates an enumerator.
        Func<TEnumerable, IEnumerable<TElement>> getEnumerable = typeShape.GetGetEnumerable();
        return new IEnumerableOfTGetterIterator<TEnumerable, TElement>(getEnumerable);
    }

    /// <summary>
    /// Creates an iterator instance from the specified dictionary shape.
    /// </summary>
    public static IIterator<TDictionary, KeyValuePair<TKey, TValue>> Create<TDictionary, TKey, TValue>(IDictionaryShape<TDictionary, TKey, TValue> shape)
        where TKey : notnull
    {
        if (typeof(Dictionary<TKey, TValue>).IsAssignableFrom(typeof(TDictionary)))
        {
            return (IIterator<TDictionary, KeyValuePair<TKey, TValue>>)(object)new DictionaryOfTIterator<TKey, TValue>();
        }

        // Default to an iterator that just allocates an enumerator.
        Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> getter = shape.GetGetDictionary();
        return new IEnumerableOfTGetterIterator<TDictionary, KeyValuePair<TKey, TValue>>(getter);
    }

    private sealed class ArrayIterator<T> : IIterator<T[], T>
    {
        public void Iterate<TState>(T[] iterable, Consumer<T, TState> iteration, ref TState state)
        {
            for (int i = 0; i < iterable.Length; i++)
            {
                iteration(iterable[i], ref state);
            }
        }
    }

    private sealed class Array2DIterator<T> : IIterator<T[,], T>
    {
        public void Iterate<TState>(T[,] iterable, Consumer<T, TState> iteration, ref TState state)
        {
            int n = iterable.GetLength(0);
            int m = iterable.GetLength(1);

            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < m; j++)
                {
                    iteration(iterable[i, j], ref state);
                }
            }
        }
    }

    private sealed class MultiDimensionalArrayIterator<TElement> : IIterator<IList, TElement>
    {
        public void Iterate<TState>(IList iterable, Consumer<TElement, TState> iteration, ref TState state)
        {
            foreach (object element in iterable)
            {
                iteration((TElement)element, ref state);
            }
        }
    }

    private sealed class ImmutableArrayIterator<T> : IIterator<ImmutableArray<T>, T>
    {
        public void Iterate<TState>(ImmutableArray<T> iterable, Consumer<T, TState> iteration, ref TState state)
        {
            for (int i = 0; i < iterable.Length; i++)
            {
                iteration(iterable[i], ref state);
            }
        }
    }

    private sealed class ListIterator<TElement> : IIterator<List<TElement>, TElement>
    {
        public void Iterate<TState>(List<TElement> iterable, Consumer<TElement, TState> iteration, ref TState state)
        {
            Span<TElement> span = CollectionsMarshal.AsSpan(iterable);
            for (int i = 0; i < span.Length; i++)
            {
                iteration(span[i], ref state);
            }
        }
    }

    private sealed class IListOfTIterator<TElement> : IIterator<IList<TElement>, TElement>
    {
        public void Iterate<TState>(IList<TElement> iterable, Consumer<TElement, TState> iteration, ref TState state)
        {
            int count = iterable.Count;
            for (int i = 0; i < count; i++)
            {
                iteration(iterable[i], ref state);
            }
        }
    }

    private sealed class IListIterator : IIterator<IList, object?>
    {
        public void Iterate<TState>(IList iterable, Consumer<object?, TState> iteration, ref TState state)
        {
            int count = iterable.Count;
            for (int i = 0; i < count; i++)
            {
                iteration(iterable[i], ref state);
            }
        }
    }

    private sealed class IEnumerableOfTIterator<TElement> : IIterator<IEnumerable<TElement>, TElement>
    {
        public void Iterate<TState>(IEnumerable<TElement> iterable, Consumer<TElement, TState> iteration, ref TState state)
        {
            foreach (TElement element in iterable)
            {
                iteration(element, ref state);
            }
        }
    }

    private sealed class MemoryOfTIterator<TElement> : IIterator<Memory<TElement>, TElement>
    {
        public void Iterate<TState>(Memory<TElement> iterable, Consumer<TElement, TState> iteration, ref TState state)
        {
            Span<TElement> span = iterable.Span;
            for (int i = 0; i < span.Length; i++)
            {
                iteration(span[i], ref state);
            }
        }
    }

    private sealed class ReadOnlyMemoryOfTIterator<TElement> : IIterator<ReadOnlyMemory<TElement>, TElement>
    {
        public void Iterate<TState>(ReadOnlyMemory<TElement> iterable, Consumer<TElement, TState> iteration, ref TState state)
        {
            ReadOnlySpan<TElement> span = iterable.Span;
            for (int i = 0; i < span.Length; i++)
            {
                iteration(span[i], ref state);
            }
        }
    }

    private sealed class IEnumerableOfTGetterIterator<TIterable, TElement> : IIterator<TIterable, TElement>
    {
        private readonly Func<TIterable, IEnumerable<TElement>> _getEnumerable;
        public IEnumerableOfTGetterIterator(Func<TIterable, IEnumerable<TElement>> getEnumerable)
            => _getEnumerable = getEnumerable;

        public void Iterate<TState>(TIterable iterable, Consumer<TElement, TState> iteration, ref TState state)
        {
            foreach (TElement element in _getEnumerable(iterable))
            {
                iteration(element, ref state);
            }
        }
    }

    private sealed class IEnumerableIterator : IIterator<IEnumerable, object?>
    {
        public void Iterate<TState>(IEnumerable iterable, Consumer<object?, TState> iteration, ref TState state)
        {
            var enumerator = iterable.GetEnumerator();
            while (enumerator.MoveNext())
            {
                iteration(enumerator.Current, ref state);
            }
        }
    }

    private sealed class DictionaryOfTIterator<TKey, TValue> : IIterator<Dictionary<TKey, TValue>, KeyValuePair<TKey, TValue>>
        where TKey : notnull
    {
        public void Iterate<TState>(Dictionary<TKey, TValue> iterable, Consumer<KeyValuePair<TKey, TValue>, TState> iteration, ref TState state)
        {
            foreach (KeyValuePair<TKey, TValue> element in iterable)
            {
                iteration(element, ref state);
            }
        }
    }
}
