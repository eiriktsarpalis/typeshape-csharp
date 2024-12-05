using PolyType.Abstractions;
using PolyType.Utilities;

namespace PolyType.Examples.RandomGenerator;

/// <summary>A delegate that generates an instance from a <see cref="Random"/> seed.</summary>
public delegate T RandomGenerator<T>(Random random, int size);

/// <summary>Provides a random generator for .NET types built on top of PolyType.</summary>
public static partial class RandomGenerator
{
    private static readonly MultiProviderTypeCache s_converterCaches = new()
    {
        DelayedValueFactory = new DelayedRandomGeneratorFactory(),
        ValueBuilderFactory = ctx => new Builder(ctx),
    };

    /// <summary>
    /// Builds a <see cref="RandomGenerator{T}"/> instance from the specified shape.
    /// </summary>
    /// <typeparam name="T">The type for which to build the converter.</typeparam>
    /// <param name="shape">The shape instance guiding printer construction.</param>
    /// <returns>An <see cref="RandomGenerator{T}"/> instance.</returns>
    public static RandomGenerator<T> Create<T>(ITypeShape<T> shape) =>
        (RandomGenerator<T>)s_converterCaches.GetOrAdd(shape)!;

    /// <summary>
    /// Builds a <see cref="RandomGenerator{T}"/> instance from the specified shape provider.
    /// </summary>
    /// <typeparam name="T">The type for which to build the converter.</typeparam>
    /// <param name="shapeProvider">The shape provider guiding printer construction.</param>
    /// <returns>An <see cref="RandomGenerator{T}"/> instance.</returns>
    public static RandomGenerator<T> Create<T>(ITypeShapeProvider shapeProvider) =>
        (RandomGenerator<T>)s_converterCaches.GetOrAdd(typeof(T), shapeProvider)!;

    /// <summary>
    /// Generates a random value using the specified parameters.
    /// </summary>
    /// <typeparam name="T">The type of the value to be randomly generated.</typeparam>
    /// <param name="generator">The random generator delegate.</param>
    /// <param name="size">The size metric of the generated value.</param>
    /// <param name="seed">The random seed used to generate the value.</param>
    /// <returns>A randomly generated value.</returns>
    public static T GenerateValue<T>(this RandomGenerator<T> generator, int size, int? seed = null)
    {
        if (size < 0)
        {
            Throw();
            static void Throw() => throw new ArgumentOutOfRangeException(nameof(size));
        }

        Random random = CreateRandom(seed);
        return generator(random, size);
    }

    /// <summary>
    /// Generates an infinite sequence of randomly generated values using the specified parameters.
    /// </summary>
    /// <typeparam name="T">The type of the value to be randomly generated.</typeparam>
    /// <param name="generator">The random generator delegate.</param>
    /// <param name="seed">The random seed used to generate the value.</param>
    /// <param name="minSize">The minimum size metric for the generated values.</param>
    /// <param name="maxSize">The maximum size metric for the generated values.</param>
    /// <returns>An infinite sequence of randomly generated values.</returns>
    public static IEnumerable<T> GenerateValues<T>(this RandomGenerator<T> generator, int? seed = null, int? minSize = null, int? maxSize = null)
    {
        if (minSize < 0 || minSize > maxSize)
        {
            Throw();
            static void Throw() => throw new ArgumentOutOfRangeException(nameof(minSize));
        }

        int size = minSize ?? 64;
        int max = maxSize ?? 4096;
        Random random = CreateRandom(seed);

        while (true)
        {
            yield return generator(random, size);
            if (size < max)
            {
                size++;
            }
        }
    }

    private static Random CreateRandom(int? seed) => seed is null
#if NET
        ? Random.Shared
#else
        ? new Random()
#endif
        : new Random(seed.Value);

#if NET
    /// <summary>
    /// Generates a random value using its <see cref="IShapeable{T}"/> implementation.
    /// </summary>
    /// <typeparam name="T">The type of the value to be randomly generated.</typeparam>
    /// <param name="size">The size metric of the generated value.</param>
    /// <param name="seed">The random seed used to generate the value.</param>
    /// <returns>A randomly generated value.</returns>
    public static T GenerateValue<T>(int size, int? seed = null) where T : IShapeable<T> => 
        RandomGeneratorCache<T, T>.Value.GenerateValue(size, seed);

    /// <summary>
    /// Generates an infinite sequence of randomly generated values using its <see cref="IShapeable{T}"/> implementation.
    /// </summary>
    /// <typeparam name="T">The type of the value to be randomly generated.</typeparam>
    /// <param name="seed">The random seed used to generate the value.</param>
    /// <param name="minSize">The minimum size metric for the generated values.</param>
    /// <param name="maxSize">The maximum size metric for the generated values.</param>
    /// <returns>An infinite sequence of randomly generated values.</returns>
    public static IEnumerable<T> GenerateValues<T>(int? seed = null, int? minSize = null, int? maxSize = null) where T : IShapeable<T> => 
        RandomGeneratorCache<T, T>.Value.GenerateValues(seed, minSize, maxSize);

    /// <summary>
    /// Generates a random value using an externally provided <see cref="IShapeable{T}"/> implementation.
    /// </summary>
    /// <typeparam name="T">The type of the value to be randomly generated.</typeparam>
    /// <typeparam name="TProvider">The type providing an <see cref="IShapeable{T}"/> implementation.</typeparam>
    /// <param name="size">The size metric of the generated value.</param>
    /// <param name="seed">The random seed used to generate the value.</param>
    /// <returns>A randomly generated value.</returns>
    public static T GenerateValue<T, TProvider>(int size, int? seed = null) where TProvider : IShapeable<T> => 
        RandomGeneratorCache<T, TProvider>.Value.GenerateValue(size, seed);

    /// <summary>
    /// Generates an infinite sequence of randomly generated values an externally provided <see cref="IShapeable{T}"/> implementation.
    /// </summary>
    /// <typeparam name="T">The type of the value to be randomly generated.</typeparam>
    /// <typeparam name="TProvider">The type providing an <see cref="IShapeable{T}"/> implementation.</typeparam>
    /// <param name="seed">The random seed used to generate the value.</param>
    /// <param name="minSize">The minimum size metric for the generated values.</param>
    /// <param name="maxSize">The maximum size metric for the generated values.</param>
    /// <returns>An infinite sequence of randomly generated values.</returns>
    public static IEnumerable<T> GenerateValues<T, TProvider>(int? seed = null, int? minSize = null, int? maxSize = null) where TProvider : IShapeable<T> =>
        RandomGeneratorCache<T, TProvider>.Value.GenerateValues(seed, minSize, maxSize);

    private static class RandomGeneratorCache<T, TProvider> where TProvider : IShapeable<T>
    {
        public static RandomGenerator<T> Value => s_value ??= Create(TProvider.GetShape());
        private static RandomGenerator<T>? s_value;
    }
#endif
}
