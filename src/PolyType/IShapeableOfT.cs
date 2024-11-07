using PolyType.Abstractions;

namespace PolyType;

/// <summary>
/// Defines a static abstract factory providing a <see cref="ITypeShape{T}"/> instance.
/// </summary>
/// <typeparam name="T">The type shape provided by this implementation.</typeparam>
public interface IShapeable<T>
{
    /// <summary>
    /// Gets the PolyType instance corresponding to <typeparamref name="T"/>.
    /// </summary>
    /// <returns>The shape for <typeparamref name="T"/>.</returns>
    static abstract ITypeShape<T> GetShape();
}