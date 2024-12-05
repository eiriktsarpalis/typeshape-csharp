using PolyType.Examples.StructuralEquality;
using Xunit;

namespace PolyType.Tests;

public abstract partial class StructuralEqualityTests(ProviderUnderTest providerUnderTest)
{
    [Theory]
    [MemberData(nameof(TestTypes.GetEqualValuePairs), MemberType = typeof(TestTypes))]
    public void EqualityComparer_EqualValues<T>(TestCase<T> left, TestCase<T> right)
    {
        if (!typeof(T).IsValueType && typeof(T) != typeof(string))
        {
            // ensure we're not using reference equality
            if (left.Value is null)
            {
                Assert.Null(right.Value);
            }
            else
            {
                Assert.NotSame((object?)left.Value, (object?)right.Value);
            }
        }

        IEqualityComparer<T> cmp = GetEqualityComparerUnderTest<T>();

        if (left.Value is not null)
        {
            Assert.Equal(cmp.GetHashCode(left.Value!), cmp.GetHashCode(right.Value!));
        }

        Assert.Equal(left.Value, right.Value, cmp!);
        Assert.Equal(right.Value, left.Value, cmp!);
    }

    [Theory]
    [MemberData(nameof(GetNotEqualValues))]
    public void EqualityComparer_NotEqualValues<T>(T left, T right)
    {
        IEqualityComparer<T> cmp = GetEqualityComparerUnderTest<T>();
        
        Assert.NotEqual(left, right, cmp!);
        Assert.NotEqual(right, left, cmp!);
    }

    public static IEnumerable<object?[]> GetNotEqualValues()
    {
        yield return NotEqual(false, true);
        yield return NotEqual(null, "");
        yield return NotEqual(-1, 4);
        yield return NotEqual(3.14, -7.5);
        yield return NotEqual(DateTime.MinValue, DateTime.MaxValue);
        yield return NotEqual((int[])[1, 2, 3], []);
        yield return NotEqual((int[])[1, 2, 3], [1, 2, 0]);
        yield return NotEqual<int[][]>(
            [[1, 0, 0], [0, 1, 0], [0, 0, 1]],
            [[1, 0, 0], [0, 0, 0], [0, 0, 1]]);

        yield return NotEqual(
            new Dictionary<string, int> { ["key1"] = 42, ["key2"] = -1 },
            new Dictionary<string, int> { ["key1"] = 42, ["key2"] = 1 });

        yield return NotEqual(
            new Dictionary<string, int> { ["key1"] = 42, ["key5"] = -1 },
            new Dictionary<string, int> { ["key1"] = 42, ["key2"] = -1 });

        yield return NotEqual(
            new DerivedClass { X = 1, Y = 2 },
            new DerivedClass { X = 1, Y = -1 });

        yield return NotEqual(
            new MyLinkedList<int>
            {
                Value = 1,
                Next = new()
                {
                    Value = 2,
                    Next = new()
                    {
                        Value = 3,
                        Next = null,
                    }
                }
            },
            new MyLinkedList<int>
            {
                Value = 1,
                Next = new()
                {
                    Value = 2,
                    Next = null
                }
            });

        static object?[] NotEqual<T>(T left, T right) => [left, right];
    }

    private IEqualityComparer<T> GetEqualityComparerUnderTest<T>() =>
        StructuralEqualityComparer.Create<T>(providerUnderTest.Provider);
}

public sealed class StructuralEqualityTests_Reflection() : StructuralEqualityTests(RefectionProviderUnderTest.NoEmit);
public sealed class StructuralEqualityTests_ReflectionEmit() : StructuralEqualityTests(RefectionProviderUnderTest.Emit);
public sealed class StructuralEqualityTests_SourceGen() : StructuralEqualityTests(SourceGenProviderUnderTest.Default);