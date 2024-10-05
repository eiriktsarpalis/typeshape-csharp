using TypeShape.Abstractions;
using TypeShape.Examples.RandomGenerator;
using TypeShape.Examples.StructuralEquality;
using Xunit;

namespace TypeShape.Tests;

public abstract class RandomGeneratorTests(IProviderUnderTest providerUnderTest)
{
    [Theory]
    [MemberData(nameof(TestTypes.GetTestCases), MemberType = typeof(TestTypes))]
    public void ProducesDeterministicRandomValues<T>(TestCase<T> testCase)
    {
        if (!testCase.HasConstructors(providerUnderTest))
        {
            return; // Random value generation not supported
        }

        (RandomGenerator<T> generator, IEqualityComparer<T> comparer) = GetGeneratorAndEqualityComparer<T>(testCase);

        const int Seed = 42;
        IEnumerable<T> firstRandomSequence = generator.GenerateValues(Seed).Take(10);
        IEnumerable<T> secondRandomSequence = generator.GenerateValues(Seed).Take(10);
        Assert.Equal(firstRandomSequence, secondRandomSequence, comparer);
    }

    private (RandomGenerator<T>, IEqualityComparer<T>) GetGeneratorAndEqualityComparer<T>(TestCase<T> testCase)
    {
        ITypeShape<T> shape = testCase.GetShape(providerUnderTest);
        return (RandomGenerator.Create(shape), StructuralEqualityComparer.Create(shape));
    }
}

public sealed class RandomGeneratorTests_Reflection() : RandomGeneratorTests(RefectionProviderUnderTest.Default);
public sealed class RandomGeneratorTests_ReflectionEmit() : RandomGeneratorTests(RefectionProviderUnderTest.NoEmit);
public sealed class RandomGeneratorTests_SourceGen() : RandomGeneratorTests(SourceGenProviderUnderTest.Default);