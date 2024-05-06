using TypeShape.Abstractions;
using TypeShape.Applications.Cloner;
using TypeShape.Applications.StructuralEquality;
using Xunit;

namespace TypeShape.Tests;

public abstract class ClonerTests(IProviderUnderTest providerUnderTest)
{
    [Theory]
    [MemberData(nameof(TestTypes.GetTestCases), MemberType = typeof(TestTypes))]
    public void Cloner_ProducesEqualCopy<T>(TestCase<T> testCase)
    {
        if (!testCase.HasConstructors(providerUnderTest))
        {
            Assert.Throws<NotSupportedException>(() => Cloner.CreateCloner(testCase.GetShape(providerUnderTest)));
            return;
        }

        (Func<T?, T?> cloner, IEqualityComparer<T> comparer) = GetClonerAndEqualityComparer<T>(testCase);

        T? clonedValue = cloner(testCase.Value);

        if (testCase.Value is null)
        {
            Assert.Null(clonedValue);
            return;
        }

        if (typeof(T) != typeof(string))
        {
            Assert.NotSame((object?)testCase.Value, (object?)clonedValue);
        }

        if (testCase.IsLossyRoundtrip)
        {
            return;
        }

        if (testCase.IsStack)
        {
            clonedValue = cloner(clonedValue);
        }

        Assert.Equal(testCase.Value, clonedValue, comparer!);
    }

    private (Func<T?, T?>, IEqualityComparer<T>) GetClonerAndEqualityComparer<T>(TestCase<T> testCase)
    {
        ITypeShape<T> shape = testCase.GetShape(providerUnderTest);
        return (Cloner.CreateCloner(shape), StructuralEqualityComparer.Create(shape));
    }
}

public sealed class ClonerTests_Reflection() : ClonerTests(RefectionProviderUnderTest.Default);
public sealed class ClonerTests_ReflectionEmit() : ClonerTests(RefectionProviderUnderTest.NoEmit);
public sealed class ClonerTests_SourceGen() : ClonerTests(SourceGenProviderUnderTest.Default);