using TypeShape.Abstractions;
using TypeShape.Applications.Cloner;
using TypeShape.Applications.StructuralEquality;
using TypeShape.ReflectionProvider;
using Xunit;

namespace TypeShape.Tests;

public abstract class ClonerTests
{
    protected abstract ITypeShapeProvider Provider { get; }

    [Theory]
    [MemberData(nameof(TestTypes.GetTestCases), MemberType = typeof(TestTypes))]
    public void Cloner_ProducesEqualCopy<T>(TestCase<T> testCase)
    {
        if (!testCase.HasConstructors(Provider))
        {
            return;
        }

        (Func<T?, T?> cloner, IEqualityComparer<T> comparer) = GetClonerAndEqualityComparer<T>();

        if (!testCase.HasConstructors(Provider))
        {
            Assert.Throws<NotSupportedException>(() => cloner(testCase.Value));
            return;
        }

        T? clonedValue = cloner(testCase.Value);

        if (typeof(T) != typeof(string))
        {
            Assert.NotSame((object?)testCase.Value, (object?)clonedValue);
        }

        if (testCase.IsStack)
        {
            Assert.Equal(testCase.Value, cloner(clonedValue), comparer!);
        }
        else if (!testCase.DoesNotRoundtrip)
        {
            Assert.Equal(testCase.Value, clonedValue, comparer!);
        }
    }

    private (Func<T?, T?>, IEqualityComparer<T>) GetClonerAndEqualityComparer<T>()
    {
        ITypeShape<T> shape = Provider.Resolve<T>();
        return (Cloner.CreateCloner(shape), StructuralEqualityComparer.Create(shape));
    }
}

public sealed class ClonerTests_Reflection : ClonerTests
{
    protected override ITypeShapeProvider Provider { get; } = new ReflectionTypeShapeProvider(useReflectionEmit: false);
}

public sealed class ClonerTests_ReflectionEmit : ClonerTests
{
    protected override ITypeShapeProvider Provider { get; } = new ReflectionTypeShapeProvider(useReflectionEmit: true);
}

public sealed class ClonerTests_SourceGen : ClonerTests
{
    protected override ITypeShapeProvider Provider { get; } = SourceGenProvider.Default;
}
