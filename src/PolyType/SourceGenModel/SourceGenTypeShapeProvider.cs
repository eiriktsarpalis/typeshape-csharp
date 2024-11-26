using PolyType.Abstractions;

namespace PolyType.SourceGenModel;

/// <summary>
/// Defines a source generated <see cref="ITypeShapeProvider"/> implementation.
/// </summary>
public abstract class SourceGenTypeShapeProvider : ITypeShapeProvider
{
    /// <summary>
    /// Gets a <see cref="ITypeShape"/> instance corresponding to the supplied type.
    /// </summary>
    /// <param name="type">The type for which a shape is requested.</param>
    /// <returns>
    /// A <see cref="ITypeShape"/> instance corresponding to the current type.
    /// </returns>
    public abstract ITypeShape? GetShape(Type type);

    /// <summary>
    /// Gets a <see cref="ITypeShape{T}"/> instance corresponding to the supplied type.
    /// </summary>
    /// <typeparam name="T">The type for which a shape is requested.</typeparam>
    /// <returns>
    /// A <see cref="ITypeShape{T}"/> instance corresponding to the current type.
    /// </returns>
    public ITypeShape<T>? GetShape<T>() => (ITypeShape<T>?)GetShape(typeof(T));
}
