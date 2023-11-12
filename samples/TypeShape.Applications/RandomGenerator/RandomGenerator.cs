namespace TypeShape.Applications.RandomGenerator;

public delegate T RandomGenerator<T>(Random random, int size);

public static partial class RandomGenerator
{
    public static RandomGenerator<T> Create<T>(ITypeShape<T> shape)
    {
        var visitor = new Visitor();
        return (RandomGenerator<T>)shape.Accept(visitor, null)!;
    }

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
            if (size < max) size++;
        }
    }
}
