using TypeShape.Abstractions;

namespace TypeShape;

/// <summary>
/// Defines a static abstract factory providing <see cref="ITypeShape"/> instances for a given type.
/// </summary>
/// <typeparam name="T">The type shape provided by this implementation.</typeparam>
public interface IShapeable<T>
{
    /// <summary>
    /// Gets the TypeShape instance corresponding to <typeparamref name="T"/>.
    /// </summary>
    /// <returns>The shape for <typeparamref name="T"/>.</returns>
    static abstract ITypeShape<T> GetShape();
}