using PolyType.Abstractions;
using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.ExceptionServices;

namespace PolyType.Utilities;

/// <summary>
/// Defines a thread-safe cache that keys values on <see cref="ITypeShape"/> instances.
/// </summary>
/// <remarks>
/// Facilitates workflows common to generating values during type graph traversal,
/// including support delayed value creation in case of recursive types.
/// </remarks>
public sealed partial class TypeCache : IReadOnlyDictionary<Type, object?>
{
    private readonly ConcurrentDictionary<Type, Entry> _cache = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="TypeCache"/> class.
    /// </summary>
    /// <param name="provider">The shape provider associated with the current cache.</param>
    public TypeCache(ITypeShapeProvider provider)
    {
        Throw.IfNull(provider);
        Provider = provider;
    }

    internal TypeCache(ITypeShapeProvider provider, MultiProviderTypeCache multiProviderCache)
    {
        Provider = provider;
        MultiProviderCache = multiProviderCache;
        ValueBuilderFactory = multiProviderCache.ValueBuilderFactory;
        DelayedValueFactory = multiProviderCache.DelayedValueFactory;
        CacheExceptions = multiProviderCache.CacheExceptions;
    }

    /// <summary>
    /// Gets the <see cref="ITypeShapeProvider"/> associated with the current cache.
    /// </summary>
    public ITypeShapeProvider Provider { get; }

    /// <summary>
    /// A factory method governing the creation of values when invoking the <see cref="GetOrAdd(ITypeShape)" /> method.
    /// </summary>
    public Func<TypeGenerationContext, ITypeShapeFunc>? ValueBuilderFactory { get; init; }

    /// <summary>
    /// A factory method governing value initialization in case of recursive types.
    /// </summary>
    public IDelayedValueFactory? DelayedValueFactory { get; init; }

    /// <summary>
    /// Specifies whether exceptions should be cached.
    /// </summary>
    public bool CacheExceptions { get; init; }

    /// <summary>
    /// Gets the global cache to which this instance belongs.
    /// </summary>
    public MultiProviderTypeCache? MultiProviderCache { get; }

    /// <summary>
    /// Creates a new <see cref="TypeGenerationContext"/> instance for the cache.
    /// </summary>
    /// <returns>A new <see cref="TypeGenerationContext"/> instance for the cache.</returns>
    public TypeGenerationContext CreateGenerationContext() => new(this);

    /// <summary>
    /// Gets the total number of entries in the cache.
    /// </summary>
    public int Count => _cache.Count;

    /// <summary>
    /// Determines whether the cache contains a value for the specified type.
    /// </summary>
    /// <param name="type">The key type.</param>
    /// <returns><see langword="true"/> is found, or <see langword="false"/> otherwise.</returns>
    public bool ContainsKey(Type type) => _cache.ContainsKey(type);

    /// <summary>
    /// Gets the value associated with the specified type.
    /// </summary>
    /// <param name="type">The type to look up.</param>
    /// <returns>The value associated with the specified key.</returns>
    public object? this[Type type] => _cache[type].GetValueOrException();

    /// <summary>
    /// Attempts to get the value associated with the specified type.
    /// </summary>
    /// <param name="type">The type key whose value to get.</param>
    /// <param name="value">When this method returns, contains the value associated with the specified type, if the type is found; otherwise, the default value for the type of the value parameter. This parameter is passed uninitialized.</param>
    /// <returns><see langword="true"/> if the cache contains an element with the specified type; otherwise, <see langword="false"/>.</returns>
    public bool TryGetValue(Type type, out object? value)
    {
        if (_cache.TryGetValue(type, out Entry entry))
        {
            value = entry.GetValueOrThrowException();
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Gets or adds a value keyed on the type represented by <paramref name="type"/>.
    /// </summary>
    /// <param name="type">The key type.</param>
    /// <returns>The final computed value.</returns>
    public object? GetOrAdd(Type type)
    {
        Throw.IfNull(type);

        if (_cache.TryGetValue(type, out Entry entry))
        {
            return entry.GetValueOrThrowException();
        }

        return AddValue(Provider.Resolve(type));
    }

    /// <summary>
    /// Gets or adds a value keyed on the type represented by <paramref name="typeShape"/>.
    /// </summary>
    /// <param name="typeShape">The type shape representing the key type.</param>
    /// <returns>The final computed value.</returns>
    public object? GetOrAdd(ITypeShape typeShape)
    {
        Throw.IfNull(typeShape);

        if (_cache.TryGetValue(typeShape.Type, out Entry entry))
        {
            return entry.GetValueOrThrowException();
        }

        return AddValue(typeShape);
    }

    private object? AddValue(ITypeShape typeShape)
    {
        ValidateProvider(typeShape.Provider);

        // Uses optimistic concurrency when committing values to the cache.
        // If conflicting entries are found in the cache, the value is re-evaluated.
        while (true)
        {
            TypeGenerationContext context = CreateGenerationContext();
            object? value;
            try
            {
                value = typeShape.Invoke(context, null);
            }
            catch (Exception ex) when (CacheExceptions)
            {
                Add(typeShape.Type, ExceptionDispatchInfo.Capture(ex));
                throw;
            }

            if (context.TryCommitResults())
            {
                return value;
            }

            if (_cache.TryGetValue(typeShape.Type, out Entry entry))
            {
                return entry.GetValueOrThrowException();
            }
        }
    }

    internal object LockObject => _cache;
    internal void Add(Type type, object? value)
    {
        Debug.Assert(Monitor.IsEntered(LockObject), "Must be called within a lock.");
        bool result = _cache.TryAdd(type, new Entry(value));
        Debug.Assert(result || ReferenceEquals(_cache[type].Value, value), "should only be pre-populated with the same value.");
    }

    internal void Add(Type type, ExceptionDispatchInfo exceptionDispatchInfo)
    {
        lock (LockObject)
        {
            _cache.TryAdd(type, new Entry(exceptionDispatchInfo));
        }
    }

    internal void ValidateProvider(ITypeShapeProvider provider)
    {
        if (!ReferenceEquals(Provider, provider))
        {
            throw new ArgumentException("The specified shape provider is not valid for this cache,", nameof(provider));
        }
    }

    private readonly struct Entry
    {
        public readonly object? Value;
        public readonly ExceptionDispatchInfo? Exception;
        public Entry(object? value) => Value = value;
        public Entry(ExceptionDispatchInfo exception) => Exception = exception;
        public object? GetValueOrThrowException()
        {
            Exception?.Throw();
            return Value;
        }

        public object? GetValueOrException() => Exception is { } e ? e : Value;
    }

    IEnumerable<Type> IReadOnlyDictionary<Type, object?>.Keys => _cache.Keys;
    IEnumerable<object?> IReadOnlyDictionary<Type, object?>.Values => _cache.Values.Select(e => e.GetValueOrException());
    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<KeyValuePair<Type, object?>>)this).GetEnumerator();
    IEnumerator<KeyValuePair<Type, object?>> IEnumerable<KeyValuePair<Type, object?>>.GetEnumerator() =>
        _cache.Select(kvp => new KeyValuePair<Type, object?>(kvp.Key, kvp.Value.GetValueOrException())).GetEnumerator();
}
