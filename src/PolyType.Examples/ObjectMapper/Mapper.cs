using PolyType.Abstractions;
using PolyType.Utilities;
using System.Diagnostics.CodeAnalysis;

namespace PolyType.Examples.ObjectMapper;

/// <summary>
/// Maps an object of type <typeparamref name="TSource"/> to an object of type <typeparamref name="TTarget"/>.
/// </summary>
/// <typeparam name="TSource">The type to map from.</typeparam>
/// <typeparam name="TTarget">The map to type to.</typeparam>
/// <param name="source">The source value from which data is mapped.</param>
/// <returns>A new value whose data is mapped from <paramref name="source"/>.</returns>
[return: NotNullIfNotNull(nameof(source))]
public delegate TTarget? Mapper<in TSource, out TTarget>(TSource? source);

/// <summary>
/// Provides a simplistic object mapping functionality.
/// </summary>
public static partial class Mapper
{
    private static readonly MultiProviderTypeCache s_cache = new()
    {
        DelayedValueFactory = new DelayedMapperFactory(),
        ValueBuilderFactory = ctx => new Builder(ctx),
    };

    /// <summary>
    /// Derives a mapper delegate from a pair of type shapes.
    /// </summary>
    /// <typeparam name="TSource">The type to map from.</typeparam>
    /// <typeparam name="TTarget">The type to map to.</typeparam>
    /// <param name="sourceShape">The shape of the type to map from.</param>
    /// <param name="targetShape">The shape of the type to map to.</param>
    /// <returns>A mapper delegate.</returns>
    public static Mapper<TSource, TTarget> Create<TSource, TTarget>(ITypeShape<TSource> sourceShape, ITypeShape<TTarget> targetShape)
    {
        TypeCache providerScopedTypeCache = s_cache.GetScopedCache(sourceShape.Provider);
        if (providerScopedTypeCache.TryGetValue(typeof(Mapper<TSource, TTarget>), out object? result))
        {
            return (Mapper<TSource, TTarget>)result!;
        }

        ITypeShape mapperShape = new MapperShape<TSource, TTarget>(sourceShape, targetShape);
        return (Mapper<TSource, TTarget>)providerScopedTypeCache.GetOrAdd(mapperShape)!;
    }

    /// <summary>
    /// Derives a mapper delegate from a PolyType provider.
    /// </summary>
    /// <typeparam name="TSource">The type to map from.</typeparam>
    /// <typeparam name="TTarget">The type to map to.</typeparam>
    /// <param name="shapeProvider">The PolyType provider.</param>
    /// <returns>A mapper delegate.</returns>
    public static Mapper<TSource, TTarget> Create<TSource, TTarget>(ITypeShapeProvider shapeProvider) =>
        Create(shapeProvider.Resolve<TSource>(), shapeProvider.Resolve<TTarget>());

    /// <summary>
    /// Maps an object of type <typeparamref name="TSource"/> to an object of type <typeparamref name="TTarget"/>.
    /// </summary>
    /// <typeparam name="TSource">The type to map from.</typeparam>
    /// <typeparam name="TTarget">The map to type to.</typeparam>
    /// <param name="source">The source value from which data is mapped.</param>
    /// <returns>A new value whose data is mapped from <paramref name="source"/>.</returns>
    [return: NotNullIfNotNull(nameof(source))]
    public static TTarget? MapValue<TSource, TTarget>(TSource? source)
        where TTarget : IShapeable<TTarget>
        where TSource : IShapeable<TSource>
        => MapperCache<TSource, TTarget>.Value(source);

    private static class MapperCache<TSource, TTarget>
        where TSource : IShapeable<TSource>
        where TTarget : IShapeable<TTarget>
    {
        public static Mapper<TSource, TTarget> Value => s_value ??= Create(TSource.GetShape(), TTarget.GetShape());
        private static Mapper<TSource, TTarget>? s_value;
    }
}