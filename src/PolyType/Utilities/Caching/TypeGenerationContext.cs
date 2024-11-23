using PolyType.Abstractions;
using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PolyType.Utilities;

/// <summary>
/// Defines a thread-local context for generating values requiring type graph traversal.
/// </summary>
public sealed partial class TypeGenerationContext : IReadOnlyDictionary<Type, object?>, ITypeShapeFunc
{
    /// <summary>The local cache used to store results during the generation process.</summary>
    private readonly Dictionary<Type, Entry> _entries = new();
    private int _totalCompletedEntries;

    /// <summary>
    /// Initializes a new instance of the <see cref="TypeGenerationContext"/> class.
    /// </summary>
    public TypeGenerationContext() { }

    internal TypeGenerationContext(TypeCache parentCache)
    {
        ParentCache = parentCache;
        DelayedValueFactory = parentCache.DelayedValueFactory;
        ValueBuilder = parentCache.ValueBuilderFactory?.Invoke(this);
    }

    /// <summary>
    /// A parent cache to which the completed results can eventually be committed.
    /// </summary>
    public TypeCache? ParentCache { get; }

    /// <summary>
    /// A factory method governing the creation of values when invoking the <see cref="GetOrAdd" /> method.
    /// </summary>
    public ITypeShapeFunc? ValueBuilder { get; init; }

    /// <summary>
    /// A factory method governing value initialization in case of recursive types.
    /// </summary>
    public IDelayedValueFactory? DelayedValueFactory { get; init; }

    /// <summary>
    /// Gets the number of entries in the local context.
    /// </summary>
    public int Count => _totalCompletedEntries;

    /// <summary>
    /// Determines whether the cache contains a value for the specified type.
    /// </summary>
    /// <param name="type">The key type.</param>
    /// <returns><see langword="true"/> is found, or <see langword="false"/> otherwise.</returns>
    public bool ContainsKey(Type type) => _entries.TryGetValue(type, out Entry entry) && entry.Kind is EntryKind.CompletedValue;

    /// <summary>
    /// Gets the value associated with the specified type.
    /// </summary>
    /// <param name="type">The type to look up.</param>
    /// <returns>The value associated with the specified key.</returns>
    public object? this[Type type] =>
        _entries.TryGetValue(type, out Entry entry) && entry.Kind is EntryKind.CompletedValue
        ? entry.Value : throw new KeyNotFoundException();

    /// <summary>
    /// Gets or adds a value keyed on the type represented by <paramref name="typeShape"/>.
    /// </summary>
    /// <typeparam name="TKey">The type key to be looked up.</typeparam>
    /// <param name="typeShape">The type shape representing the key type.</param>
    /// <param name="state">The state object to be passed to the visitor.</param>
    /// <returns>The final computed value.</returns>
    public object? GetOrAdd<TKey>(ITypeShape<TKey> typeShape, object? state = null)
    {
        ArgumentNullException.ThrowIfNull(typeShape);
        if (ValueBuilder is null)
        {
            throw new InvalidOperationException($"Calling this method requires specifying a {ValueBuilder} property.");
        }

        if (TryGetValue<TKey>(typeShape, out object? value))
        {
            return value;
        }

        value = typeShape.Invoke(ValueBuilder, state);
        Add(typeShape.Type, value);
        return value;
    }

    /// <summary>
    /// Looks up the value for type <typeparamref name="TKey"/>.
    /// </summary>
    /// <typeparam name="TKey">The type key to be looked up.</typeparam>
    /// <param name="typeShape">The type shape representing the key type.</param>
    /// <param name="value">The value returned by the lookup operation.</param>
    /// <returns>True if either a completed or delayed value have been returned.</returns>
    public bool TryGetValue<TKey>(ITypeShape<TKey> typeShape, [MaybeNullWhen(false)] out object? value)
    {
        ArgumentNullException.ThrowIfNull(typeShape);
        ParentCache?.ValidateProvider(typeShape.Provider);

        // Consult the parent cache first to avoid creating duplicate values.
        if (ParentCache?.TryGetValue(typeof(TKey), out value) is true)
        {
            return true;
        }

        ref Entry entryRef = ref CollectionsMarshal.GetValueRefOrNullRef(_entries, typeof(TKey));
        if (Unsafe.IsNullRef(ref entryRef))
        {
            if (DelayedValueFactory is not null)
            {
                // First time visiting this type, add an empty entry to the
                // dictionary to record that it has been visited and return false.
                _entries[typeof(TKey)] = new(EntryKind.Empty);
            }

            value = default;
            return false;
        }

        switch (entryRef.Kind)
        {
            case EntryKind.Empty:
                // Second time visiting this type without a value being created, we have a recursive type.
                Debug.Assert(DelayedValueFactory is not null);

                // Create a delayed value and return the uninitialized result.
                DelayedValue delayedValue = DelayedValueFactory.Create<TKey>(typeShape);
                value = delayedValue.PotentiallyDelayedResult;
                entryRef = new(EntryKind.DelayedValue, delayedValue);
                return true;

            case EntryKind.DelayedValue:
                // The stored value is an uninitialized result, return it.
                value = ((DelayedValue)entryRef.Value!).PotentiallyDelayedResult;
                return true;

            default:
                Debug.Assert(entryRef.Kind is EntryKind.CompletedValue);

                // A completed value is being stored, return it.
                value = entryRef.Value;
                return true;
        }
    }

    /// <summary>
    /// Adds a new entry to the dictionary, completing any delayed values for the key type.
    /// </summary>
    /// <param name="key">The key type of the new entry.</param>
    /// <param name="value">The value of the new entry.</param>
    /// <param name="overwrite">Whether to overwrite existing entries.</param>
    public void Add(Type key, object? value, bool overwrite = false)
    {
        ArgumentNullException.ThrowIfNull(key);

        ref Entry entryRef = ref CollectionsMarshal.GetValueRefOrAddDefault(_entries, key, out bool _);

        switch (entryRef.Kind)
        {
            case EntryKind.Empty:
                // Missing or empty entry, update it with the value.
                entryRef = new(EntryKind.CompletedValue, value);
                _totalCompletedEntries++;
                break;

            case EntryKind.DelayedValue:
                // The existing entry is a delayed value from recursive occurrences,
                // complete it and then replace the entry with the actual value.
                ((DelayedValue)entryRef.Value!).CompleteValue(value);
                entryRef = new(EntryKind.CompletedValue, value);
                _totalCompletedEntries++;
                break;

            default:
                Debug.Assert(entryRef.Kind is EntryKind.CompletedValue);

                if (!overwrite)
                {
                    throw new InvalidOperationException($"A key of type '{key}' has already been added to the cache.");
                }

                // The entry is a regular value, overwrite it.
                entryRef = new(EntryKind.CompletedValue, value);
                break;
        }
    }

    /// <summary>
    /// Attempts to commit the results of the generation context to the parent cache.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if all values were committed successfully,
    /// or <see langword="false"/> if no values were committed due to conflicts.
    /// </returns>
    /// <exception cref="InvalidOperationException">Instance does not specify a <see cref="ParentCache"/>.</exception>
    /// <remarks>
    /// In case of a failed commit operation due to conflicts, the current results should be discarded and retried.
    /// </remarks>
    public bool TryCommitResults()
    {
        if (ParentCache is null)
        {
            throw new InvalidOperationException("The current generation context does not specify a parent cache to commit results to.");
        }

        lock (ParentCache.LockObject)
        {
            // Iterate over the entries twice, once to validate and once to write.
            // This is to avoid writing partial results to the parent cache.

            foreach (KeyValuePair<Type, Entry> entry in _entries)
            {
                // Validate that local entries are all fully computed.
                if (entry.Value.Kind is not EntryKind.CompletedValue)
                {
                    throw new InvalidOperationException($"The type '{entry.Key}' has a delayed value that has not been completed.");
                }

                // Validate that the parent cache does not already contain a different value.
                if (ParentCache.TryGetValue(entry.Key, out object? conflict) &&
                    !ReferenceEquals(entry.Value.Value, conflict))
                {
                    return false;
                }
            }

            // Validation complete; commit the results to the parent cache.
            foreach (KeyValuePair<Type, Entry> entry in _entries)
            {
                ParentCache.Add(entry.Key, entry.Value.Value);
            }

            return true;
        }
    }

    /// <summary>
    /// Clears the local cache.
    /// </summary>
    public void Clear()
    {
        _entries.Clear();
        _totalCompletedEntries = 0;
    }

    private readonly struct Entry(EntryKind state, object? value = null)
    {
        public EntryKind Kind { get; } = state;
        public object? Value { get; } = value;
    }

    private enum EntryKind
    {
        Empty = 0,
        DelayedValue = 1,
        CompletedValue = 2,
    }

    object? ITypeShapeFunc.Invoke<T>(ITypeShape<T> typeShape, object? state) => GetOrAdd(typeShape, state);

    bool IReadOnlyDictionary<Type, object?>.TryGetValue(Type key, out object? value)
    {
        if (_entries.TryGetValue(key, out Entry entry) && entry.Kind is EntryKind.CompletedValue)
        {
            value = entry.Value;
            return true;
        }

        value = default;
        return false;
    }

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<KeyValuePair<Type, object?>>)this).GetEnumerator();

    IEnumerator<KeyValuePair<Type, object?>> IEnumerable<KeyValuePair<Type, object?>>.GetEnumerator() =>
        GetCompletedValues().Select(kvp => new KeyValuePair<Type, object?>(kvp.Key, kvp.Value.Value)).GetEnumerator();
    IEnumerable<Type> IReadOnlyDictionary<Type, object?>.Keys => GetCompletedValues().Select(kvp => kvp.Key);
    IEnumerable<object?> IReadOnlyDictionary<Type, object?>.Values => GetCompletedValues().Select(kvp => kvp.Value.Value);
    private IEnumerable<KeyValuePair<Type, Entry>> GetCompletedValues() => _entries.Where(kvp => kvp.Value.Kind is EntryKind.CompletedValue);
}