using PolyType.Abstractions;
using PolyType.Utilities;

namespace PolyType.Examples.Cloner;

/// <summary>
/// Provides an object graph deep cloning implementation built on top of PolyType.
/// </summary>
public static partial class Cloner
{
    private static readonly MultiProviderTypeCache s_clonerCache = new()
    {
        DelayedValueFactory = new DelayedClonerFactory(),
        ValueBuilderFactory = ctx => new Builder(ctx),
    };

    /// <summary>
    /// Builds a deep cloning delegate from the specified shape.
    /// </summary>
    /// <typeparam name="T">The type for which to build the cloner.</typeparam>
    /// <param name="shape">The shape instance guiding cloner construction.</param>
    /// <returns>A delegate for cloning instances of type <typeparamref name="T"/>.</returns>
    public static Func<T?, T?> CreateCloner<T>(ITypeShape<T> shape) =>
        (Func<T?, T?>)s_clonerCache.GetOrAdd(shape)!;

    /// <summary>
    /// Builds a deep cloning delegate from the specified shape provider.
    /// </summary>
    /// <typeparam name="T">The type for which to build the cloner.</typeparam>
    /// <param name="shapeProvider">The shape provider guiding cloner construction.</param>
    /// <returns>A delegate for cloning instances of type <typeparamref name="T"/>.</returns>
    public static Func<T?, T?> CreateCloner<T>(ITypeShapeProvider shapeProvider) =>
        (Func<T?, T?>)s_clonerCache.GetOrAdd(typeof(T), shapeProvider)!;

    /// <summary>
    /// Deep clones an instance of type <typeparamref name="T"/> using its <see cref="ITypeShape{T}"/> implementation.
    /// </summary>
    /// <typeparam name="T">The type of the value to be cloned.</typeparam>
    /// <param name="value">The value to be cloned.</param>
    /// <returns>A deep cloned copy of <paramref name="value"/>.</returns>
    public static T? Clone<T>(T? value) where T : IShapeable<T> =>
        ClonerCache<T, T>.Value(value);

    /// <summary>
    /// Deep clones an instance of type <typeparamref name="T"/> using an externally provider <see cref="ITypeShape{T}"/> implementation.
    /// </summary>
    /// <typeparam name="T">The type of the value to be cloned.</typeparam>
    /// <typeparam name="TProvider">The type providing an <see cref="IShapeable{T}"/> implementation.</typeparam>
    /// <param name="value">The value to be cloned.</param>
    /// <returns>A deep cloned copy of <paramref name="value"/>.</returns>
    public static T? Clone<T, TProvider>(T? value) where TProvider : IShapeable<T> =>
        ClonerCache<T, TProvider>.Value(value);

    private static class ClonerCache<T, TProvider> where TProvider : IShapeable<T>
    {
        public static Func<T?, T?> Value => s_value ??= CreateCloner(TProvider.GetShape());
        private static Func<T?, T?>? s_value;
    }
}