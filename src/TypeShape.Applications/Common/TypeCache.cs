using System.Diagnostics;
using System.Runtime.InteropServices;

namespace TypeShape.Applications;

/// <summary>
/// Defines a dictionary keyed on types and containing values that are of the key type.
/// </summary>
/// <remarks>
/// Used for deriving values when walking potentially recursive type graphs.
/// </remarks>
public sealed class TypeCache
{
    private readonly Dictionary<Type, object> _cache;

    /// <summary>
    /// Creates an empty instance.
    /// </summary>
    public TypeCache()
    {
        _cache = new();
    }

    /// <summary>
    /// Creates an instance prepopulated with entries.
    /// </summary>
    /// <param name="values">Entries with which to pre-populate the cache.</param>
    /// <exception cref="ArgumentException">Provided entries are not of expected type.</exception>
    public TypeCache(IEnumerable<KeyValuePair<Type, object>> values)
    {
        _cache = new(values.Select(ValidateEntry));

        static KeyValuePair<Type, object> ValidateEntry(KeyValuePair<Type, object> entry)
        {
            if (!entry.Key.IsAssignableFrom(entry.Value.GetType()))
            {
                ThrowArgumentException();
                void ThrowArgumentException()
                    => throw new ArgumentException($"Value of entry with key {entry.Key} is not assignable to the key.", nameof(values));
            }

            return entry;
        }
    }

    /// <summary>
    /// Gets the cached value of type <typeparamref name="T"/> or installs a delayed value that can be returned on recursive calls.
    /// </summary>
    /// <typeparam name="T">The type to look up.</typeparam>
    /// <param name="delayedValueFactory">A factory method used to create a delayed value.</param>
    /// <returns>
    /// An instance of type <typeparamref name="T"/> if already cached or if method has already been called. 
    /// Otherwise, returns <see langword="null"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="delayedValueFactory"/> is null or returns null.</exception>
    public T? GetOrAddDelayedValue<T>(Func<ResultHolder<T>, T> delayedValueFactory) where T : class
    {
        ArgumentNullException.ThrowIfNull(delayedValueFactory);

        ref object? entryRef = ref CollectionsMarshal.GetValueRefOrAddDefault(_cache, typeof(T), out bool exists);
        if (!exists)
        {
            entryRef = new ResultHolderCore<T>(delayedValueFactory);
            return null;
        }
        else
        {
            Debug.Assert(entryRef is T or ResultHolderCore<T>);
            return entryRef is T t ? t : ((ResultHolderCore<T>)entryRef!).GetDelayedResult();
        }
    }

    /// <summary>
    /// Adds a value of type <typeparamref name="T"/> to the cache.
    /// </summary>
    /// <typeparam name="T">The type of the value to be added.</typeparam>
    /// <param name="value">The value of <typeparamref name="T"/> to be added.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    /// <exception cref="InvalidOperationException">A key of type <typeparamref name="T"/> has already been added.</exception>
    public void Add<T>(T value) where T : class
    {
        ArgumentNullException.ThrowIfNull(value);

        ref object? entryRef = ref CollectionsMarshal.GetValueRefOrAddDefault(_cache, typeof(T), out bool exists);
        if (!exists)
        {
            entryRef = value;
        }
        else
        {
            Debug.Assert(entryRef is T or ResultHolderCore<T>);
            if (entryRef is ResultHolderCore<T> { Value: null } resultHolder)
            {
                resultHolder.CompleteValue(value);
                entryRef = value;
            }
            else
            {
                throw new InvalidOperationException($"A key of type {typeof(T)} has already been added to the cache.");
            }
        }
    }

    private sealed class ResultHolderCore<T>  : ResultHolder<T>
        where T : class
    {
        private readonly Func<ResultHolder<T>, T> _delayedValueFactory;
        private T? _delayedValue;

        public ResultHolderCore(Func<ResultHolder<T>, T> delayedValueFactory)
            => _delayedValueFactory = delayedValueFactory;

        public T GetDelayedResult()
        {
            return _delayedValue ??= GetDelayedResultCore();
            T GetDelayedResultCore()
            {
                T delayedValue = _delayedValueFactory(this);
                return delayedValue is null ? throw new ArgumentNullException("delayedValueFactory") : delayedValue;
            }
        }

        public void CompleteValue(T value)
        {
            _delayedValue = Value = value;
        }
    }
}

/// <summary>
/// Defines a cell containing a value with delayed initialization.
/// </summary>
/// <typeparam name="T">The type of the contained value.</typeparam>
public abstract class ResultHolder<T> where T : class
{
    internal ResultHolder() { }

    /// <summary>
    /// Gets the contained value if populated.
    /// </summary>
    public T? Value { get; private protected set; }
}
