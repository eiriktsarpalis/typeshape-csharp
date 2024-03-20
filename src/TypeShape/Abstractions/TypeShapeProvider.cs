namespace TypeShape;

/// <summary>
/// Helper methods for extracting <see cref="ITypeShape"/> instances from types.
/// </summary>
public static class TypeShapeProvider
{
    /// <summary>
    /// Extracts the <see cref="ITypeShape"/> description provided by the given type.
    /// </summary>
    /// <typeparam name="T">The type from which to extract the shape.</typeparam>
    /// <returns>An <see cref="ITypeShape"/> instance describing <typeparamref name="T"/>.</returns>
    public static ITypeShape<T> GetShape<T>() where T : ITypeShapeProvider<T>
        => T.GetShape();

    /// <summary>
    /// Extracts the <see cref="ITypeShape"/> description provided by the given type.
    /// </summary>
    /// <typeparam name="T">The type for which to extract the shape.</typeparam>
    /// <typeparam name="TProvider">The type from which to extract the shape.</typeparam>
    /// <returns>An <see cref="ITypeShape"/> instance describing <typeparamref name="T"/>.</returns>
    public static ITypeShape<T> GetShape<T, TProvider>() where TProvider : ITypeShapeProvider<T>
        => TProvider.GetShape();
}