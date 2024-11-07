using PolyType.Examples.Counter;
using Xunit;

namespace PolyType.Tests;

public abstract partial class CounterTests(IProviderUnderTest providerUnderTest)
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
        SourceGenProvider p = SourceGenProvider.Default;
        yield return Create(p, "string", 1);
        yield return Create(p, (string?)null, 0);
        yield return Create(p, -5, 1);
        yield return Create(p, false, 1);
        yield return Create(p, (int[])[1, 2, 3, 4, 5], 6);
        yield return Create(p, (string?[])[null, "str", "str", null, null], 3);
        yield return Create(p, (List<int>)[1, 2, 3, 4, 5], 6);
        yield return Create(p, new Dictionary<string, int> { ["k1"] = 1, ["k2"] = 2 }, 5);
        yield return Create(default(SimpleRecord), new SimpleRecord(42), 2);
        yield return Create(p, new MyLinkedList<SimpleRecord> { Value = new SimpleRecord(1), Next = new() { Value = new SimpleRecord(2), Next = null } }, 6);

        static object?[] Create<TProvider, T>(TProvider? provider, T? value, long expectedCount)
            where TProvider : IShapeable<T> => 
            [TestCase.Create(provider, value), expectedCount];
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

    protected Func<T?, long> GetCounterUnderTest<T>(TestCase<T> testCase) => Counter.Create<T>(providerUnderTest.ResolveShape(testCase));
}

public sealed class CounterTests_Reflection() : CounterTests(RefectionProviderUnderTest.Default);
public sealed class CounterTests_ReflectionEmit() : CounterTests(RefectionProviderUnderTest.NoEmit);
public sealed class CounterTests_SourceGen() : CounterTests(SourceGenProviderUnderTest.Default);