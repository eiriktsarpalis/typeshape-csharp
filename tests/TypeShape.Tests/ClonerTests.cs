using TypeShape.Abstractions;
using TypeShape.Examples.Cloner;
using TypeShape.Examples.StructuralEquality;
using Xunit;

namespace TypeShape.Tests;

public abstract class ClonerTests(IProviderUnderTest providerUnderTest)
{
    [Theory]
    [MemberData(nameof(TestTypes.GetTestCases), MemberType = typeof(TestTypes))]
    public void Cloner_ProducesEqualCopy<T>(TestCase<T> testCase)
    {
        if (!providerUnderTest.HasConstructor(testCase))
        {
            Assert.Throws<NotSupportedException>(() => Cloner.CreateCloner(providerUnderTest.ResolveShape(testCase)));
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

        if (testCase.IsStack)
        {
            clonedValue = cloner(clonedValue);
        }

        Assert.Equal(testCase.Value, clonedValue, comparer!);
    }

    private (Func<T?, T?>, IEqualityComparer<T>) GetClonerAndEqualityComparer<T>(TestCase<T> testCase)
    {
        ITypeShape<T> shape = providerUnderTest.ResolveShape(testCase);
        return (Cloner.CreateCloner(shape), StructuralEqualityComparer.Create(shape));
    }
}

public sealed class ClonerTests_Reflection() : ClonerTests(RefectionProviderUnderTest.Default);
public sealed class ClonerTests_ReflectionEmit() : ClonerTests(RefectionProviderUnderTest.NoEmit);
public sealed class ClonerTests_SourceGen() : ClonerTests(SourceGenProviderUnderTest.Default);