using PolyType.Abstractions;
using PolyType.Utilities;

namespace PolyType.Examples.Counter;

/// <summary>
/// A simple proof-of-concept implementation that uses PolyType to count the number of nodes in an object graph.
/// </summary>
public static partial class Counter
{
    private static readonly MultiProviderTypeCache s_cache = new()
    {
        DelayedValueFactory = new DelayedCounterFactory(),
        ValueBuilderFactory = ctx => new Builder(ctx),
    };

    /// <summary>
    /// Creates an object counting delegate using the specified shape.
    /// </summary>
    public static Func<T?, long> Create<T>(ITypeShape<T> shape)
        => (Func<T?, long>)s_cache.GetOrAdd(shape)!;

    /// <summary>
    /// Creates an object counting delegate using the specified shape provider.
    /// </summary>
    public static Func<T?, long> Create<T>(ITypeShapeProvider shapeProvider)
        => Create(shapeProvider.Resolve<T>());

    /// <summary>
    /// Counts the number of nodes in the object graph using its <see cref="IShapeable{T}"/> implementation.
    /// </summary>
    public static long GetCount<T>(T? value) where T : IShapeable<T>
        => CounterCache<T, T>.Value(value);

    /// <summary>
    /// Counts the number of nodes in the object graph using an externally provided <see cref="IShapeable{T}"/> implementation.
    /// </summary>
    public static long GetCount<T, TProvider>(T? value) where TProvider : IShapeable<T>
        => CounterCache<T, TProvider>.Value(value);

    private static class CounterCache<T, TProvider> where TProvider : IShapeable<T>
    {
        public static Func<T?, long> Value => s_value ??= Create(TProvider.GetShape());
        private static Func<T?, long>? s_value;
    }
}
