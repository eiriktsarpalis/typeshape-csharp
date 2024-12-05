using PolyType.Examples.Counter;
using Xunit;

namespace PolyType.Tests;

public abstract partial class CounterTests(ProviderUnderTest providerUnderTest)
{
    [Theory]
    [MemberData(nameof(GetValuesAndExpectedResult))]
    public void ReturnsExpectedCount<T>(TestCase<T> testCase, long expectedCount)
    {
        Func<T?, long> counter = GetCounterUnderTest(testCase);

        long result = counter(testCase.Value);

        Assert.Equal(expectedCount, result);
    }

    public static IEnumerable<object?[]> GetValuesAndExpectedResult()
    {
        ITypeShapeProvider p = Witness.ShapeProvider;
        yield return Create(TestCase.Create("string", p), 1);
        yield return Create(TestCase.Create((string?)null, p), 0);
        yield return Create(TestCase.Create(-5, p), 1);
        yield return Create(TestCase.Create(false, p), 1);
        yield return Create(TestCase.Create((int[])[1, 2, 3, 4, 5], p), 6);
        yield return Create(TestCase.Create((string?[])[null, "str", "str", null, null], p), 3);
        yield return Create(TestCase.Create((List<int>)[1, 2, 3, 4, 5], p), 6);
        yield return Create(TestCase.Create(new Dictionary<string, int> { ["k1"] = 1, ["k2"] = 2 }, p), 5);
        yield return Create(TestCase.Create(new SimpleRecord(42), p), 2);
        yield return Create(TestCase.Create(new MyLinkedList<SimpleRecord> { Value = new SimpleRecord(1), Next = new() { Value = new SimpleRecord(2), Next = null } }, p), 6);

        static object?[] Create<T>(TestCase<T> testCase, long expectedCount) => [testCase, expectedCount];
    }

    [Theory]
    [MemberData(nameof(TestTypes.GetEqualValuePairs), MemberType = typeof(TestTypes))]
    public void EqualValuesReturnEqualCount<T>(TestCase<T> left, TestCase<T> right)
    {
        Func<T?, long> counter = GetCounterUnderTest(left);

        long leftCount = counter(left.Value);
        long rightCount = counter(right.Value);

        Assert.Equal(leftCount, rightCount);
    }

    protected Func<T?, long> GetCounterUnderTest<T>(TestCase<T> testCase) => Counter.Create(providerUnderTest.ResolveShape(testCase));
}

public sealed class CounterTests_Reflection() : CounterTests(RefectionProviderUnderTest.NoEmit);
public sealed class CounterTests_ReflectionEmit() : CounterTests(RefectionProviderUnderTest.Emit);
public sealed class CounterTests_SourceGen() : CounterTests(SourceGenProviderUnderTest.Default);