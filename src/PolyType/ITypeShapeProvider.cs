using PolyType.Abstractions;

namespace PolyType;

/// <summary>
/// Defines a provider for <see cref="ITypeShape"/> implementations.
/// </summary>
public interface ITypeShapeProvider
{
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
