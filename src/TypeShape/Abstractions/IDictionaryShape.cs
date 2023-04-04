using System.Collections;

namespace TypeShape;

/// <summary>
/// Provides a strongly-typed shape model for a .NET type that is a dictionary.
/// </summary>
/// <remarks>
/// Typically covers types implementing interfaces such as <see cref="IDictionary{TKey, TValue}"/>,
/// <see cref="IReadOnlyDictionary{TKey, TValue}"/> or <see cref="IDictionary"/>.
/// </remarks>
public interface IDictionaryShape
{
    /// <summary>
    /// The shape of the underlying dictionary type.
    /// </summary>
    ITypeShape Type { get; }

    /// <summary>
    /// The shape of the underlying key type.
    /// </summary>
    /// <remarks>
    /// For non-generic dictionaries this returns <see cref="ITypeShape{object}"/>.
    /// </remarks>
    ITypeShape KeyType { get; }

    /// <summary>
    /// The shape of the underlying value type.
    /// </summary>
    /// <remarks>
    /// For non-generic dictionaries this returns <see cref="ITypeShape{object}"/>.
    /// </remarks>
    ITypeShape ValueType { get; }

    /// <summary>
    /// Indicates whether instances of the enumerable type can be mutated.
    /// </summary>
    bool IsMutable { get; }

    /// <summary>
    /// Accepts an <see cref="ITypeShapeVisitor"/> for strongly-typed traversal.
    /// </summary>
    /// <param name="visitor">The visitor to accept.</param>
    /// <param name="state">The state parameter to pass to the underlying visitor.</param>
    /// <returns>The <see cref="object?"/> result returned by the visitor.</returns>
    object? Accept(ITypeShapeVisitor visitor, object? state);
}

/// <summary>
/// Provides a strongly-typed shape model for a .NET type that is a dictionary.
/// </summary>
/// <typeparam name="TDictionary">The type of the underlying dictionary.</typeparam>
/// <typeparam name="TKey">The type of the underlying key.</typeparam>
/// <typeparam name="TValue">The type of the underlying value.</typeparam>
/// <remarks>
/// Typically covers types implementing interfaces such as <see cref="IDictionary{TKey, TValue}"/>,
/// <see cref="IReadOnlyDictionary{TKey, TValue}"/> or <see cref="IDictionary"/>.
/// </remarks>
public interface IDictionaryShape<TDictionary, TKey, TValue> : IDictionaryShape
    where TKey : notnull
{
    /// <summary>
    /// Creates a delegate used for getting a <see cref="IReadOnlyDictionary{TKey, TValue}"/>
    /// view of the enumerable.
    /// </summary>
    /// <returns>
    /// A delegate accepting a <typeparamref name="TDictionary"/> and 
    /// returning an <see cref="IReadOnlyDictionary{TKey, TValue}"/> view of the instance.
    /// </returns>
    Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> GetGetDictionary();

    /// <summary>
    /// Creates a setter delegate used for appending an 
    /// <see cref="KeyValuePair{TKey, TValue}"/> to a mutable dictionary.
    /// </summary>
    /// <exception cref="InvalidOperationException">The dictionary does not support mutation.</exception>
    /// <returns>A setter delegate used for appending entries to a mutable dictionary.</returns>
    Setter<TDictionary, KeyValuePair<TKey, TValue>> GetAddKeyValuePair();
}
