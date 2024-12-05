namespace PolyType.Abstractions;

/// <summary>
/// Helper methods for extracting <see cref="ITypeShape"/> instances from shape providers.
/// </summary>
public static class TypeShapeProvider
{
#if NET
    /// <summary>
    /// Resolves the <see cref="ITypeShape"/> provided by the given type.
    /// </summary>
    /// <typeparam name="T">The type from which to extract the shape.</typeparam>
    /// <returns>An <see cref="ITypeShape"/> instance describing <typeparamref name="T"/>.</returns>
    public static ITypeShape<T> Resolve<T>() where T : IShapeable<T>
        => T.GetShape();

    /// <summary>
    /// Resolves the <see cref="ITypeShape"/> provided by the given type.
    /// </summary>
    /// <typeparam name="T">The type for which to extract the shape.</typeparam>
    /// <typeparam name="TProvider">The type from which to extract the shape.</typeparam>
    /// <returns>An <see cref="ITypeShape"/> instance describing <typeparamref name="T"/>.</returns>
    public static ITypeShape<T> Resolve<T, TProvider>() where TProvider : IShapeable<T>
        => TProvider.GetShape();
#endif

    /// <summary>
    /// Resolves the <see cref="ITypeShape{T}"/> corresponding to <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The ty</typeparam>
    /// <param name="shapeProvider">The provider from which to extract the <see cref="ITypeShape"/>.</param>
    /// <returns>An <see cref="ITypeShape{T}"/> instance describing <typeparamref name="T"/>.</returns>
    /// <exception cref="NotSupportedException"><paramref name="shapeProvider"/> does not support <typeparamref name="T"/>.</exception>
    public static ITypeShape<T> Resolve<T>(this ITypeShapeProvider shapeProvider)
        => (ITypeShape<T>)Resolve(shapeProvider, typeof(T));

    /// <summary>
    /// Resolves the <see cref="ITypeShape"/> corresponding to <paramref name="type"/>.
    /// </summary>
    /// <param name="shapeProvider">The provider from which to extract the <see cref="ITypeShape"/>.</param>
    /// <param name="type">The type whose shape we need to resolve.</param>
    /// <returns>An <see cref="ITypeShape"/> instance describing <paramref name="type"/>.</returns>
    /// <exception cref="NotSupportedException"><paramref name="shapeProvider"/> does not support <paramref name="type"/>.</exception>
    public static ITypeShape Resolve(this ITypeShapeProvider shapeProvider, Type type)
        => shapeProvider.GetShape(type) ?? throw new NotSupportedException($"The shape provider '{shapeProvider.GetType()}' does not support type '{type}'.");
}