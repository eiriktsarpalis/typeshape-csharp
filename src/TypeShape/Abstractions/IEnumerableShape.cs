﻿using System.Collections;

namespace TypeShape;

/// <summary>
/// Provides a strongly-typed shape model for a .NET type that is enumerable.
/// </summary>
/// <remarks>
/// Typically covers all types implementing <see cref="IEnumerable{T}"/> or <see cref="IEnumerable"/>.
/// </remarks>
public interface IEnumerableShape
{
    /// <summary>
    /// The shape of the underlying enumerable type.
    /// </summary>
    ITypeShape Type { get; }

    /// <summary>
    /// The shape of the underlying element type.
    /// </summary>
    /// <remarks>
    /// For non-generic <see cref="IEnumerable"/> this returns the shape for <see cref="object"/>.
    /// </remarks>
    ITypeShape ElementType { get; }

    /// <summary>
    /// Gets the construction strategy for the given collection.
    /// </summary>
    CollectionConstructionStrategy ConstructionStrategy { get; }

    /// <summary>
    /// Gets the rank of the enumerable, if a multi-dimensional array.
    /// </summary>
    int Rank { get; }

    /// <summary>
    /// Accepts an <see cref="ITypeShapeVisitor"/> for strongly-typed traversal.
    /// </summary>
    /// <param name="visitor">The visitor to accept.</param>
    /// <param name="state">The state parameter to pass to the underlying visitor.</param>
    /// <returns>The <see cref="object"/> result returned by the visitor.</returns>
    object? Accept(ITypeShapeVisitor visitor, object? state);
}

/// <summary>
/// Provides a strongly-typed shape model for a .NET type that is enumerable.
/// </summary>
/// <typeparam name="TEnumerable">The type of underlying enumerable.</typeparam>
/// <typeparam name="TElement">The type of underlying element.</typeparam>
/// <remarks>
/// Typically covers all types implementing <see cref="IEnumerable{T}"/> or <see cref="IEnumerable"/>.
///
/// For non-generic collections, <typeparamref name="TElement"/> is instantiated to <see cref="object"/>.
/// </remarks>
public interface IEnumerableShape<TEnumerable, TElement> : IEnumerableShape
{
    /// <summary>
    /// The shape of the underlying enumerable type.
    /// </summary>
    new ITypeShape<TEnumerable> Type { get; }

    /// <summary>
    /// The shape of the underlying element type.
    /// </summary>
    /// <remarks>
    /// For non-generic <see cref="IEnumerable"/> this returns the shape for <see cref="object"/>.
    /// </remarks>
    new ITypeShape<TElement> ElementType { get; }

    /// <summary>
    /// Creates a delegate used for getting a <see cref="IEnumerable{TElement}"/>
    /// view of the enumerable.
    /// </summary>
    /// <returns>
    /// A delegate accepting a <typeparamref name="TEnumerable"/> and
    /// returning an <see cref="IEnumerable{TElement}"/> view of the instance.
    /// </returns>
    Func<TEnumerable, IEnumerable<TElement>> GetGetEnumerable();

    /// <summary>
    /// Creates a delegate wrapping a parameterless constructor of a mutable collection.
    /// </summary>
    /// <exception cref="InvalidOperationException">The collection is not <see cref="CollectionConstructionStrategy.Mutable"/>.</exception>
    /// <returns>A delegate wrapping a default constructor.</returns>
    Func<TEnumerable> GetDefaultConstructor();

    /// <summary>
    /// Creates a setter delegate used for appending an <typeparamref name="TElement"/> to a mutable collection.
    /// </summary>
    /// <exception cref="InvalidOperationException">The collection is not <see cref="CollectionConstructionStrategy.Mutable"/>.</exception>
    /// <returns>A setter delegate used for appending elements to a mutable collection.</returns>
    Setter<TEnumerable, TElement> GetAddElement();

    /// <summary>
    /// Creates a constructor delegate for creating a collection from a span.
    /// </summary>
    /// <exception cref="InvalidOperationException">The collection is not <see cref="CollectionConstructionStrategy.Span"/>.</exception>
    /// <returns>A delegate constructing a collection from a span of values.</returns>
    SpanConstructor<TElement, TEnumerable> GetSpanConstructor();

    /// <summary>
    /// Creates a constructor delegate for creating a collection from an enumerable.
    /// </summary>
    /// <exception cref="InvalidOperationException">The collection is not <see cref="CollectionConstructionStrategy.Enumerable"/>.</exception>
    /// <returns>A delegate constructing a collection from an enumerable of values.</returns>
    Func<IEnumerable<TElement>, TEnumerable> GetEnumerableConstructor();

    /// <inheritdoc/>
    ITypeShape IEnumerableShape.Type => Type;

    /// <inheritdoc/>
    ITypeShape IEnumerableShape.ElementType => ElementType;

    /// <inheritdoc/>
    object? IEnumerableShape.Accept(ITypeShapeVisitor visitor, object? state) => visitor.VisitEnumerable(this, state);
}