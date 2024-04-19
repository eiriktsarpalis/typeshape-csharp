using TypeShape.Abstractions;

namespace TypeShape;

/// <summary>
/// Defines a static and strongly typed <see cref="ITypeShape"/> provider abstraction.
/// </summary>
/// <typeparam name="T">The type shape provided by this implementation.</typeparam>
public interface ITypeShapeProvider<T>
{
    /// <summary>
    /// Gets the TypeShape instance corresponding to <typeparamref name="T"/>.
    /// </summary>
    /// <returns>The shape for <typeparamref name="T"/>.</returns>
    static abstract ITypeShape<T> GetShape();
}