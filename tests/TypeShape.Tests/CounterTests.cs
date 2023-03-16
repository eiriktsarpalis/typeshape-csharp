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
        yield return Create(new int[] { 1, 2, 3, 4, 5 }, 6);
        yield return Create(new List<int> { 1, 2, 3, 4, 5 }, 6);
        yield return Create(new Dictionary<string, int> { ["k1"] = 1, ["k2"] = 2 }, 5);
        yield return Create(new SimpleRecord(42), 2);
        yield return Create(new LinkedList<SimpleRecord> { Value = new SimpleRecord(1), Next = new() { Value = new SimpleRecord(2), Next = null } }, 6);

        static object?[] Create<T>(T value, long expectedCount)
            => new object?[] { value, expectedCount };
    }

    [Theory]
    [MemberData(nameof(GetEqualValues))]
    public void EqualValuesReturnEqualCount<T>(T left, T right)
    {
        Func<T, long> counter = GetCounterUnderTest<T>();

        long leftCount = counter(left);
        long rightCount = counter(right);

        Assert.Equal(leftCount, rightCount);
    }

    public static IEnumerable<object[]> GetEqualValues()
        => TestTypes.GetTestValuesCore()
            .Zip(TestTypes.GetTestValuesCore(), (l, r) => new object[] { l, r });

    protected Func<T, long> GetCounterUnderTest<T>()
    {
        IType<T>? shape = Provider.GetShape<T>();
        Assert.NotNull(shape);
        return Counter.Create(shape);
    }
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
    protected override ITypeShapeProvider Provider { get; } = SourceGenTypeShapeProvider.Default;
}
