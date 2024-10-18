using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using TypeShape.Abstractions;

namespace TypeShape.Examples.Utilities;

/// <summary>
/// Defines a dictionary that can be used to store values keyed on <see cref="Type"/>.
/// </summary>
/// <remarks>
/// Can be used for storing values while walking potentially cyclic type graphs.
/// Includes facility for delayed value computation in case of recursive types.
/// </remarks>
public class TypeDictionary : IDictionary<Type, object?>
{
    // Entries with IsCompleted: false denote types whose values are still being computed.
    // In such cases the value is either null or an instance of IResultBox representing a delayed value.
    // These values are only surfaced in lookup calls where a delayedValueFactory parameter is specified.
    private readonly Dictionary<Type, (object? Value, bool IsCompleted)> _dict = new();
    private DelayedCollection<Type>? _keyColection;
    private DelayedCollection<object?>? _valueCollection;

    /// <summary>
    /// Gets or adds a value keyed on the type represented by <paramref name="shape"/>.
    /// </summary>
    /// <typeparam name="TValue">The type of the value to be resolved.</typeparam>
    /// <param name="shape">The type shape representing the key type.</param>
    /// <param name="visitor">The type shape visitor used to compute the value.</param>
    /// <param name="delayedValueFactory">A factory used to create delayed values in case of recursive types.</param>
    /// <param name="state">The state object to be passed to the visitor.</param>
    /// <returns>The final computed value.</returns>
    public TValue GetOrAdd<TValue>(
        ITypeShape shape,
        ITypeShapeVisitor visitor,
        Func<ResultBox<TValue>, TValue>? delayedValueFactory = null,
        object? state = null)
    {
        if (TryGetValue(shape.Type, out TValue? value, delayedValueFactory))
        {
            return value;
        }

        value = (TValue)shape.Accept(visitor, state)!;
        Add(shape.Type, value);
        return value;
    }

    /// <summary>
    /// Looks up the value for type <paramref name="key"/>, or a delayed value if entering a cyclic occurrence.
    /// </summary>
    /// <typeparam name="TValue">The type of the value to be looked up.</typeparam>
    /// <param name="key">The type for which to look up the value.</param>
    /// <param name="value">The value returned by the lookup operation.</param>
    /// <param name="delayedValueFactory">A factory for creating delayed values in case of cyclic types.</param>
    /// <returns>True if either a completed or delayed value have been returned.</returns>
    public bool TryGetValue<TValue>(Type key, [MaybeNullWhen(false)] out TValue value, Func<ResultBox<TValue>, TValue>? delayedValueFactory = null)
    {
        ref (object? Entry, bool IsCompleted) entryRef = ref CollectionsMarshal.GetValueRefOrNullRef(_dict, key);
        if (Unsafe.IsNullRef(ref entryRef))
        {
            // First time visiting this type, return no result.
            if (delayedValueFactory != null)
            {
                // If we're specifying a delayed factory, add an empty entry
                // to denote that the next lookup should create a delayed value.
                _dict[key] = (default(TValue), IsCompleted: false);
            }

            value = default;
            return false;
        }
        else if (!entryRef.IsCompleted)
        {
            // Second time visiting this type without a value being computed, encountering a potential cyclic type.
            if (delayedValueFactory is null)
            {
                // If no delayed factory is specified, return no result.
                value = default;
                return false;
            }

            Debug.Assert(entryRef.Entry is null or IResultBox);

            if (entryRef.Entry is IResultBox existingResultBox)
            {
                // A delayed value has already bee created, return that.
                value = (TValue)existingResultBox.DelayedValue!;
            }
            else
            {
                // Create a new delayed value and update the entry.
                var newResultBox = new ResultBoxImpl<TValue>();
                newResultBox._delayedValue = value = delayedValueFactory(newResultBox);
                entryRef = (newResultBox, IsCompleted: false);
            }

            return true;
        }
        else
        {
            // We found a completed entry, return it.
            value = (TValue)entryRef.Entry!;
            return true;
        }
    }

    /// <summary>
    /// Adds a new entry to the dictionary, completing any delayed values for the key type.
    /// </summary>
    /// <typeparam name="TValue">The type of the value to be added.</typeparam>
    /// <param name="key">The key type of the new entry.</param>
    /// <param name="value">The value of the new entry.</param>
    /// <param name="overwrite">Whether to overwrite existing entries.</param>
    public void Add<TValue>(Type key, TValue value, bool overwrite = false)
    {
        ref (object? Entry, bool IsCompleted) entryRef = ref CollectionsMarshal.GetValueRefOrNullRef(_dict, key);

        if (Unsafe.IsNullRef(ref entryRef))
        {
            _dict[key] = (value, IsCompleted: true);
        }
        else
        {
            if (entryRef.IsCompleted && !overwrite)
            {
                throw new InvalidOperationException($"A key of type '{key}' has already been added to the cache.");
            }
            if (entryRef.Entry is IResultBox resultBox)
            {
                // Complete the delayed value with the new value.
                Debug.Assert(!entryRef.IsCompleted);
                resultBox.CompleteResult(value);
            }

            entryRef = (value, IsCompleted: true);
        }
    }

    /// <summary>
    /// Gets the total number of generated values.
    /// </summary>
    public int Count => _dict.Count(e => e.Value.IsCompleted);

    /// <summary>
    /// Checks if the specified key is present in the dictionary.
    /// </summary>
    /// <param name="key">The key to look up.</param>
    /// <returns>The value indica</returns>
    public bool ContainsKey(Type key) => _dict.TryGetValue(key, out var entry) && entry.IsCompleted;

    /// <summary>
    /// Clears the contents of the dictionary.
    /// </summary>
    public void Clear() => _dict.Clear();

    /// <summary>
    /// Removes the entry associated with the specified key.
    /// </summary>
    /// <param name="key">The key to remove.</param>
    /// <returns>A boolean indicating whether an entry was succesfully removed.</returns>
    public bool Remove(Type key) => _dict.Remove(key);

    /// <summary>
    /// Gets or sets the value associated with the specified key.
    /// </summary>
    /// <param name="key">The key to look up.</param>
    /// <returns></returns>
    /// <exception cref="KeyNotFoundException">Key not found in the dictionary.</exception>
    public object? this[Type key]
    {
        get => _dict.TryGetValue(key, out var entry) && entry.IsCompleted ? entry.Value : throw new KeyNotFoundException();
        set => Add(key, value, overwrite: true);
    }

    bool IDictionary<Type, object?>.TryGetValue(Type key, out object? value) => TryGetValue(key, out value);
    void IDictionary<Type, object?>.Add(Type key, object? value) => Add(key, value);

    bool ICollection<KeyValuePair<Type, object?>>.IsReadOnly => false;
    void ICollection<KeyValuePair<Type, object?>>.Add(KeyValuePair<Type, object?> item) => Add(item.Key, item.Value);
    bool ICollection<KeyValuePair<Type, object?>>.Contains(KeyValuePair<Type, object?> item) => _dict.Contains(new(item.Key, (item.Value, IsCompleted: true)));
    bool ICollection<KeyValuePair<Type, object?>>.Remove(KeyValuePair<Type, object?> item) => ((ICollection<KeyValuePair<Type, (object?, bool)>>)_dict).Remove(new(item.Key, (item.Value, true)));
    ICollection<Type> IDictionary<Type, object?>.Keys => _keyColection ??= new(_dict.Where(e => e.Value.IsCompleted).Select(e => e.Key));
    ICollection<object?> IDictionary<Type, object?>.Values => _valueCollection ??= new(_dict.Where(e => e.Value.IsCompleted).Select(e => e.Value.Value));
    void ICollection<KeyValuePair<Type, object?>>.CopyTo(KeyValuePair<Type, object?>[] array, int arrayIndex)
    {
        if (array.Length - arrayIndex < Count)
        {
            throw new ArgumentException("Insufficient space in the target array.", nameof(array));
        }

        foreach (KeyValuePair<Type, (object? Value, bool IsCompleted)> entry in _dict)
        {
            if (entry.Value.IsCompleted)
            {
                array[arrayIndex++] = new(entry.Key, entry.Value.Value);
            }
        }
    }

    IEnumerator<KeyValuePair<Type, object?>> IEnumerable<KeyValuePair<Type, object?>>.GetEnumerator()
    {
        foreach (KeyValuePair<Type, (object? Value, bool IsCompleted)> entry in _dict)
        {
            if (entry.Value.IsCompleted)
            {
                yield return new KeyValuePair<Type, object?>(entry.Key, entry.Value.Value);
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<KeyValuePair<Type, object?>>)this).GetEnumerator();

    private sealed class ResultBoxImpl<T> : ResultBox<T>, IResultBox
    {
        public T? _delayedValue;

        public object? DelayedValue => _delayedValue;

        public void CompleteResult(object? result)
        {
            _result = (T)result!;
            _delayedValue = default;
            IsCompleted = true;
        }
    }

    private interface IResultBox
    {
        object? DelayedValue { get; }
        void CompleteResult(object? result);
    }

    private sealed class DelayedCollection<T>(IEnumerable<T> source) : ICollection<T>
    {
        public int Count => source.Count();
        public bool IsReadOnly => true;
        public bool Contains(T item) => source.Contains(item);
        public void CopyTo(T[] array, int arrayIndex) => source.ToArray().CopyTo(array, arrayIndex);
        IEnumerator IEnumerable.GetEnumerator() => source.GetEnumerator();
        public IEnumerator<T> GetEnumerator() => source.GetEnumerator();
        public bool Remove(T item) => throw new NotSupportedException();
        public void Add(T item) => throw new NotSupportedException();
        public void Clear() => throw new NotSupportedException();
    }
}

/// <summary>
/// A container that holds the delayed result of a computation.
/// </summary>
/// <typeparam name="T">The type of the contained value.</typeparam>
public abstract class ResultBox<T>
{
    private protected T? _result;

    internal ResultBox() { }

    /// <summary>
    /// Gets a value indicating whether the result has been computed.
    /// </summary>
    public bool IsCompleted { get; private protected set; }

    /// <summary>
    /// Gets the contained result if populated.
    /// </summary>
    public T Result
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (!IsCompleted)
            {
                Throw();
                static void Throw() => throw new InvalidOperationException($"Value of type '{typeof(T)}' has not been completed yet.");
            }

            return _result!;
        }
    }
}
