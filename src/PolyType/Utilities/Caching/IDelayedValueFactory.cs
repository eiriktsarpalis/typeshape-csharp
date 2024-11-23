using PolyType.Abstractions;

namespace PolyType.Utilities;

/// <summary>
/// Defines a factory for containers of values that could be created later.
/// </summary>
public interface IDelayedValueFactory
{
    /// <summary>
    /// Creates a delayed value corresponding to the provided type.
    /// </summary>
    /// <typeparam name="T">The type corresponding to the delayed value.</typeparam>
    /// <param name="typeShape">The shape of the type corresponding to the delayed value.</param>
    /// <returns>A potentially uninitialized delayed value instance.</returns>
    DelayedValue Create<T>(ITypeShape<T> typeShape);
}
