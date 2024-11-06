using TypeShape.Examples.StructuralEquality;
using Xunit;

namespace TypeShape.Tests;

public abstract partial class StructuralEqualityTests(IProviderUnderTest providerUnderTest)
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

        IEqualityComparer<T> cmp = GetEqualityComparerUnderTest(left);

        if (left.Value is not null)
        {
            Assert.Equal(cmp.GetHashCode(left.Value!), cmp.GetHashCode(right.Value!));
        }

        Assert.Equal(left.Value, right.Value, cmp!);
        Assert.Equal(right.Value, left.Value, cmp!);
    }

    [Theory]
    [MemberData(nameof(GetNotEqualValues))]
    public void EqualityComparer_NotEqualValues<T>(TestCase<T> left, TestCase<T> right)
    {
        IEqualityComparer<T> cmp = GetEqualityComparerUnderTest(left);
        
        Assert.NotEqual(left.Value, right.Value, cmp!);
        Assert.NotEqual(right.Value, left.Value, cmp!);
    }

    public static IEnumerable<object[]> GetNotEqualValues()
    {
        SourceGenProvider p = SourceGenProvider.Default;
        yield return NotEqual(p, false, true);
        yield return NotEqual(p, null, "");
        yield return NotEqual(p, -1, 4);
        yield return NotEqual(p, 3.14, -7.5);
        yield return NotEqual(p, DateTime.MinValue, DateTime.MaxValue);
        yield return NotEqual(p, (int[])[1, 2, 3], []);
        yield return NotEqual(p, (int[])[1, 2, 3], [1, 2, 0]);
        yield return NotEqual(p,
            (int[][])[[1, 0, 0], [0, 1, 0], [0, 0, 1]],
            [[1, 0, 0], [0, 0, 0], [0, 0, 1]]);

        yield return NotEqual(p,
            new Dictionary<string, int> { ["key1"] = 42, ["key2"] = -1 },
            new Dictionary<string, int> { ["key1"] = 42, ["key2"] = 1 });

        yield return NotEqual(p,
            new Dictionary<string, int> { ["key1"] = 42, ["key5"] = -1 },
            new Dictionary<string, int> { ["key1"] = 42, ["key2"] = -1 });

        yield return NotEqual(default(DerivedClass),
            new DerivedClass { X = 1, Y = 2 },
            new DerivedClass { X = 1, Y = -1 });

        yield return NotEqual(p,
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

        static object[] NotEqual<TProvider, T>(TProvider? provider, T left, T right) where TProvider : IShapeable<T> =>
            [TestCase.Create(provider, left), TestCase.Create(provider, right)];
    }

    private IEqualityComparer<T> GetEqualityComparerUnderTest<T>(TestCase<T> testCase) =>
        StructuralEqualityComparer.Create(providerUnderTest.ResolveShape(testCase));
}

public sealed class StructuralEqualityTests_Reflection() : StructuralEqualityTests(RefectionProviderUnderTest.Default);
public sealed class StructuralEqualityTests_ReflectionEmit() : StructuralEqualityTests(RefectionProviderUnderTest.NoEmit);
public sealed class StructuralEqualityTests_SourceGen() : StructuralEqualityTests(SourceGenProviderUnderTest.Default);