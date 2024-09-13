using TypeShape.Abstractions;

namespace TypeShape.Applications.RandomGenerator;

public delegate T RandomGenerator<T>(Random random, int size);

public static partial class RandomGenerator
{
    public static RandomGenerator<T> Create<T>(ITypeShape<T> shape) => 
        new Builder().BuildGenerator(shape);

    public static RandomGenerator<T> Create<T>(ITypeShapeProvider provider) =>
        Create(provider.Resolve<T>());

    public static T GenerateValue<T>(this RandomGenerator<T> generator, int size, int? seed = null)
    {
        if (size < 0)
        {
            Throw();
            static void Throw() => throw new ArgumentOutOfRangeException(nameof(size));
        }

        Random random = seed is null ? Random.Shared : new Random(seed.Value);
        return generator(random, size);
    }

    public static IEnumerable<T> GenerateValues<T>(this RandomGenerator<T> generator, int? seed = null, int? minSize = null, int? maxSize = null)
    {
        if (minSize < 0 || minSize > maxSize)
        {
            Throw();
            static void Throw() => throw new ArgumentOutOfRangeException(nameof(minSize));
        }

        int size = minSize ?? 64;
        int max = maxSize ?? 4096;
        Random random = seed is null ? Random.Shared : new Random(seed.Value);

        while (true)
        {
            yield return generator(random, size);
            if (size < max)
            {
                size++;
            }
        }
    }

    public static T GenerateValue<T>(int size, int? seed = null) where T : ITypeShapeProvider<T> => 
        RandomGeneratorCache<T, T>.Value.GenerateValue(size, seed);

    public static IEnumerable<T> GenerateValues<T>(int? seed = null, int? minSize = null, int? maxSize = null) where T : ITypeShapeProvider<T> => 
        RandomGeneratorCache<T, T>.Value.GenerateValues(seed, minSize, maxSize);

    public static T GenerateValue<T, TProvider>(int size, int? seed = null) where TProvider : ITypeShapeProvider<T> => 
        RandomGeneratorCache<T, TProvider>.Value.GenerateValue(size, seed);

    public static IEnumerable<T> GenerateValues<T, TProvider>(int? seed = null, int? minSize = null, int? maxSize = null) where TProvider : ITypeShapeProvider<T> =>
        RandomGeneratorCache<T, TProvider>.Value.GenerateValues(seed, minSize, maxSize);

    private static class RandomGeneratorCache<T, TProvider> where TProvider : ITypeShapeProvider<T>
    {
        public static RandomGenerator<T> Value => s_value ??= Create(TProvider.GetShape());
        private static RandomGenerator<T>? s_value;
    }
}
