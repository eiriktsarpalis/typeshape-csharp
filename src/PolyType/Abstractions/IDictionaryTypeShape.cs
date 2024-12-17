using System.Collections;

namespace PolyType.Abstractions;

/// <summary>
/// Provides a strongly typed shape model for a .NET type that is a dictionary.
/// </summary>
/// <remarks>
/// Typically covers types implementing interfaces such as <see cref="IDictionary{TKey, TValue}"/>,
/// <see cref="IReadOnlyDictionary{TKey, TValue}"/> or <see cref="IDictionary"/>.
/// </remarks>
public interface IDictionaryTypeShape : ITypeShape
{
    /// <summary>
    /// Gets the shape of the underlying key type.
    /// </summary>
    /// <remarks>
    /// For non-generic dictionaries this returns the shape for <see cref="object"/>.
    /// </remarks>
    ITypeShape KeyType { get; }

    /// <summary>
    /// Gets the shape of the underlying value type.
    /// </summary>
    /// <remarks>
    /// For non-generic dictionaries this returns the shape for <see cref="object"/>.
    /// </remarks>
    ITypeShape ValueType { get; }

    /// <summary>
    /// Gets the construction strategy for the given collection.
    /// </summary>
    CollectionConstructionStrategy ConstructionStrategy { get; }
}

/// <summary>
/// Provides a strongly typed shape model for a .NET type that is a dictionary.
/// </summary>
/// <typeparam name="TDictionary">The type of the underlying dictionary.</typeparam>
/// <typeparam name="TKey">The type of the underlying key.</typeparam>
/// <typeparam name="TValue">The type of the underlying value.</typeparam>
/// <remarks>
/// Typically covers types implementing interfaces such as <see cref="IDictionary{TKey, TValue}"/>,
/// <see cref="IReadOnlyDictionary{TKey, TValue}"/> or <see cref="IDictionary"/>.
/// </remarks>
public interface IDictionaryTypeShape<TDictionary, TKey, TValue> : ITypeShape<TDictionary>, IDictionaryTypeShape
    where TKey : notnull
{
    /// <summary>
    /// Gets the shape of the underlying key type.
    /// </summary>
    /// <remarks>
    /// For non-generic dictionaries this returns the shape for <see cref="object"/>.
    /// </remarks>
    new ITypeShape<TKey> KeyType { get; }

    /// <summary>
    /// Gets the shape of the underlying value type.
    /// </summary>
    /// <remarks>
    /// For non-generic dictionaries this returns the shape for <see cref="object"/>.
    /// </remarks>
    new ITypeShape<TValue> ValueType { get; }

    /// <summary>
    /// Creates a delegate used for getting a <see cref="IReadOnlyDictionary{TKey, TValue}"/> view of the dictionary.
    /// </summary>
    /// <returns>
    /// A delegate accepting a <typeparamref name="TDictionary"/> and
    /// returning an <see cref="IReadOnlyDictionary{TKey, TValue}"/> view of the instance.
    /// </returns>
    Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> GetGetDictionary();

    /// <summary>
    /// Creates a delegate wrapping a parameterless constructor of a mutable collection.
    /// </summary>
    /// <exception cref="InvalidOperationException">The collection is not <see cref="CollectionConstructionStrategy.Mutable"/>.</exception>
    /// <returns>A delegate wrapping a default constructor.</returns>
    Func<TDictionary> GetDefaultConstructor();

    /// <summary>
    /// Creates a setter delegate used for appending a <see cref="KeyValuePair{TKey, TValue}"/> to a mutable dictionary.
    /// </summary>
    /// <exception cref="InvalidOperationException">The collection is not <see cref="CollectionConstructionStrategy.Mutable"/>.</exception>
    /// <returns>A setter delegate used for appending entries to a mutable dictionary.</returns>
    Setter<TDictionary, KeyValuePair<TKey, TValue>> GetAddKeyValuePair();

    /// <summary>
    /// Creates a constructor delegate for creating a collection from a span.
    /// </summary>
    /// <exception cref="InvalidOperationException">The collection is not <see cref="CollectionConstructionStrategy.Span"/>.</exception>
    /// <returns>A delegate constructing a collection from a span of values.</returns>
    SpanConstructor<KeyValuePair<TKey, TValue>, TDictionary> GetSpanConstructor();

    /// <summary>
    /// Creates a constructor delegate for creating a collection from an enumerable.
    /// </summary>
    /// <exception cref="InvalidOperationException">The collection is not <see cref="CollectionConstructionStrategy.Enumerable"/>.</exception>
    /// <returns>A delegate constructing a collection from an enumerable of values.</returns>
    Func<IEnumerable<KeyValuePair<TKey, TValue>>, TDictionary> GetEnumerableConstructor();
}
