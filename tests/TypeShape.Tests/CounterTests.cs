using TypeShape.Abstractions;
using TypeShape.Applications.Counter;
using TypeShape.ReflectionProvider;
using Xunit;

namespace TypeShape.Tests;

public abstract class CounterTests
{
    protected abstract ITypeShapeProvider Provider { get; }

    [Theory]
    [MemberData(nameof(GetValuesAndExpectedResult))]
    public void ReturnsExpectedCount<T>(T value, long expectedCount)
    {
        Func<T, long> counter = GetCounterUnderTest<T>();

        long result = counter(value);

        Assert.Equal(expectedCount, result);
    }

    public static IEnumerable<object?[]> GetValuesAndExpectedResult()
    {
        yield return Create("string", 1);
        yield return Create(-5, 1);
        yield return Create(false, 1);
        yield return Create<int[]>([1, 2, 3, 4, 5], 6);
        yield return Create<List<int>>([1, 2, 3, 4, 5], 6);
        yield return Create(new Dictionary<string, int> { ["k1"] = 1, ["k2"] = 2 }, 5);
        yield return Create(new SimpleRecord(42), 2);
        yield return Create(new MyLinkedList<SimpleRecord> { Value = new SimpleRecord(1), Next = new() { Value = new SimpleRecord(2), Next = null } }, 6);

        static object?[] Create<T>(T value, long expectedCount) => [value, expectedCount];
    }

    [Theory]
    [MemberData(nameof(TestTypes.GetEqualValuePairs), MemberType = typeof(TestTypes))]
    public void EqualValuesReturnEqualCount<T>(TestCase<T> left, TestCase<T> right)
    {
        Func<T?, long> counter = GetCounterUnderTest<T>();

        long leftCount = counter(left.Value);
        long rightCount = counter(right.Value);

        Assert.Equal(leftCount, rightCount);
    }

    protected Func<T?, long> GetCounterUnderTest<T>() => Counter.Create<T>(Provider);
}

public class CounterTests_Reflection : CounterTests
{
    protected override ITypeShapeProvider Provider { get; } = new ReflectionTypeShapeProvider(useReflectionEmit: false);
}

public class CounterTests_ReflectionEmit : CounterTests
{
    protected override ITypeShapeProvider Provider { get; } = new ReflectionTypeShapeProvider(useReflectionEmit: true);
}

public class CounterTests_SourceGen : CounterTests
{
    [Theory]
    [MemberData(nameof(TestTypes.GetTestCases), MemberType = typeof(TestTypes))]
    public void TypeShapeProvider_ReturnsExpectedCount<T, TProvider>(TestCase<T, TProvider> value) where TProvider : ITypeShapeProvider<T>
    {
        long expectedResult = Counter.Create(ReflectionTypeShapeProvider.Default.GetShape<T>())(value.Value);

        long result = Counter.GetCount<T, TProvider>(value.Value);

        Assert.Equal(expectedResult, result);
    }

    protected override ITypeShapeProvider Provider { get; } = SourceGenProvider.Default;
}
