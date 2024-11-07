namespace PolyType.Abstractions;

/// <summary>
/// A generic function used to unpack <see cref="ITypeShape"/> instances.
/// </summary>
/// <remarks>
/// Represents a rank-2 function that unpacks the existential type encoded by <see cref="ITypeShape"/>.
/// </remarks>
public interface ITypeShapeFunc
{
    /// <summary>
    /// Invokes the function using the specified <paramref name="typeShape"/> and <paramref name="state"/>.
    /// </summary>
    /// <typeparam name="T">The type represented by <paramref name="typeShape"/>.</typeparam>
    /// <param name="typeShape">The generic shape representation.</param>
    /// <param name="state">The state to be passed to the generic function.</param>
    /// <returns>The result of the generic function.</returns>
    object? Invoke<T>(ITypeShape<T> typeShape, object? state = null);
}
