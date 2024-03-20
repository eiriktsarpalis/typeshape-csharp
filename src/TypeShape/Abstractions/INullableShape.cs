namespace TypeShape;

/// <summary>
/// Provides a strongly-typed shape model for a .NET <see cref="Nullable{T}"/> type.
/// </summary>
public interface INullableShape
{
    /// <summary>
    /// The shape of the element type of the nullable.
    /// </summary>
    ITypeShape ElementType { get; }

    /// <summary>
    /// Accepts an <see cref="ITypeShapeVisitor"/> for strongly-typed traversal.
    /// </summary>
    /// <param name="visitor">The visitor to accept.</param>
    /// <param name="state">The state parameter to pass to the underlying visitor.</param>
    /// <returns>The <see cref="object"/> result returned by the visitor.</returns>
    object? Accept(ITypeShapeVisitor visitor, object? state);
}

/// <summary>
/// Provides a strongly-typed shape model for a .NET <see cref="Nullable{T}"/> type.
/// </summary>
/// <typeparam name="T">The element type of the nullable.</typeparam>
public interface INullableShape<T> : INullableShape
    where T : struct
{
    /// <summary>
    /// The shape of the element type of the nullable.
    /// </summary>
    new ITypeShape<T> ElementType { get; }

    /// <inheritdoc/>
    ITypeShape INullableShape.ElementType => ElementType;

    /// <inheritdoc/>
    object? INullableShape.Accept(ITypeShapeVisitor visitor, object? state) => visitor.VisitNullable(this, state);
}