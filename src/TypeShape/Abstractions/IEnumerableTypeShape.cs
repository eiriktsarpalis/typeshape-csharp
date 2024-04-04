using System.Collections;

namespace TypeShape;

/// <summary>
/// Provides a strongly typed shape model for a .NET type that is enumerable.
/// </summary>
/// <remarks>
/// Typically covers all types implementing <see cref="IEnumerable{T}"/> or <see cref="IEnumerable"/>.
/// </remarks>
public interface IEnumerableTypeShape : ITypeShape
{
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

    /// <inheritdoc/>
    TypeShapeKind ITypeShape.Kind => TypeShapeKind.Enumerable;
}

/// <summary>
/// Provides a strongly typed shape model for a .NET type that is enumerable.
/// </summary>
/// <typeparam name="TEnumerable">The type of underlying enumerable.</typeparam>
/// <typeparam name="TElement">The type of underlying element.</typeparam>
/// <remarks>
/// Typically covers all types implementing <see cref="IEnumerable{T}"/> or <see cref="IEnumerable"/>.
///
/// For non-generic collections, <typeparamref name="TElement"/> is instantiated to <see cref="object"/>.
/// </remarks>
public interface IEnumerableTypeShape<TEnumerable, TElement> : ITypeShape<TEnumerable>, IEnumerableTypeShape
{
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
    ITypeShape IEnumerableTypeShape.ElementType => ElementType;

    /// <inheritdoc/>
    object? ITypeShape.Accept(ITypeShapeVisitor visitor, object? state) => visitor.VisitEnumerable(this, state);
}