namespace TypeShape;

/// <summary>
/// Abstracts a provider for <see cref="ITypeShape"/> implementations.
/// </summary>
public interface ITypeShapeProvider
{
    /// <summary>
    /// Gets a <see cref="ITypeShape{T}"/> instance corresponding to the supplied type.
    /// </summary>
    /// <typeparam name="T">The type for which a shape is requested.</typeparam>
    /// <returns>
    /// A <see cref="ITypeShape{T}"/> instance corresponding to the current type,
    /// or <see langword="null" /> if a shape is not available.
    /// </returns>
    ITypeShape<T>? GetShape<T>();

    /// <summary>
    /// Gets a <see cref="ITypeShape"/> instance corresponding to the supplied type.
    /// </summary>
    /// <param name="type">The type for which a shape is requested.</param>
    /// <returns>
    /// A <see cref="ITypeShape"/> instance corresponding to the current type,
    /// or <see langword="null" /> if a shape is not available.
    /// </returns>
    ITypeShape? GetShape(Type type);
}
