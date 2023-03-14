using TypeShape.Applications.RandomGenerator;
using TypeShape.Applications.StructuralEquality;
using TypeShape.ReflectionProvider;
using Xunit;

namespace TypeShape.Tests;

public abstract class RandomGeneratorTests
{
    protected abstract ITypeShapeProvider Provider { get; }

    [Theory]
    [MemberData(nameof(TestTypes.GetTestValues), MemberType = typeof(TestTypes))]
    public void ProducesDeterministicRandomValues<T>(T value)
    {
        _ = value; // ignore the value
        (RandomGenerator<T> generator, IEqualityComparer<T> comparer) = GetGeneratorAndEqualityComparer<T>();

        const int Seed = 42;
        IEnumerable<T> firstRandomSequence = generator.GenerateValues(Seed).Take(10);
        IEnumerable<T> secondRandomSequence = generator.GenerateValues(Seed).Take(10);
        Assert.Equal(firstRandomSequence, secondRandomSequence, comparer);
    }

    private (RandomGenerator<T>, IEqualityComparer<T>) GetGeneratorAndEqualityComparer<T>()
    {
        IType<T>? shape = Provider.GetShape<T>();
        Assert.NotNull(shape);
        return (RandomGenerator.Create(shape), StructuralEqualityComparer.Create(shape));
    }
}

public sealed class RandomGeneratorTests_Reflection : RandomGeneratorTests
{
    protected override ITypeShapeProvider Provider { get; } = new ReflectionTypeShapeProvider(useReflectionEmit: false);
}

public sealed class RandomGeneratorTests_ReflectionEmit : RandomGeneratorTests
{
    protected override ITypeShapeProvider Provider { get; } = new ReflectionTypeShapeProvider(useReflectionEmit: true);
}

public sealed class RandomGeneratorTests_SourceGen : RandomGeneratorTests
{
    protected override ITypeShapeProvider Provider { get; } = SourceGenTypeShapeProvider.Default;
}
