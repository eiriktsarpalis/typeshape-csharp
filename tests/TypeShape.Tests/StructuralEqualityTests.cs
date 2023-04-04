using TypeShape.Applications.StructuralEquality;
using TypeShape.ReflectionProvider;
using Xunit;

namespace TypeShape.Tests;

public abstract class StructuralEqualityTests
{
    protected abstract ITypeShapeProvider Provider { get; }

    [Theory]
    [MemberData(nameof(GetEqualValues))]
    public void EqualityComparer_EqualValues<T>(TestCase<T> left, TestCase<T> right)
    {
        if (!typeof(T).IsValueType && typeof(T) != typeof(string))
        {
            Assert.NotSame((object)left.Value!, (object)right.Value!); // ensure we're not using reference equality
        }

        IEqualityComparer<T> cmp = GetEqualityComparerUnderTest<T>();
        Assert.Equal(cmp.GetHashCode(left.Value!), cmp.GetHashCode(right.Value!));
        Assert.Equal(left.Value, right.Value, cmp);

        Assert.Equal(cmp.GetHashCode(right.Value!), cmp.GetHashCode(left.Value!));
        Assert.Equal(right.Value, left.Value, cmp);
    }

    public static IEnumerable<object[]> GetEqualValues()
        => TestTypes.GetTestCasesCore()
            .Zip(TestTypes.GetTestCasesCore(), (l, r) => new object[] { l, r });

    [Theory]
    [MemberData(nameof(GetNotEqualValues))]
    public void EqualityComparer_NotEqualValues<T>(T left, T right)
    {
        IEqualityComparer<T> cmp = GetEqualityComparerUnderTest<T>();
        Assert.NotEqual(left, right, cmp);
        Assert.NotEqual(right, left, cmp);
    }

    public static IEnumerable<object[]> GetNotEqualValues()
    {
        yield return NotEqual(false, true);
        yield return NotEqual(null, "");
        yield return NotEqual(-1, 4);
        yield return NotEqual(3.14, -7.5);
        yield return NotEqual(DateTime.MinValue, DateTime.MaxValue);
        yield return NotEqual(new int[] { 1,2,3}, Array.Empty<int>());
        yield return NotEqual(new int[] { 1, 2, 3 }, new int[] { 1, 2, 0 });
        yield return NotEqual(
            new int[][] { new int[] { 1, 0, 0 }, new int[] { 0, 1, 0 }, new int[] { 0, 0, 1 } },
            new int[][] { new int[] { 1, 0, 0 }, new int[] { 0, 0, 0 }, new int[] { 0, 0, 1 } });

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
            new LinkedList<int>
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
            new LinkedList<int>
            {
                Value = 1,
                Next = new()
                {
                    Value = 2,
                    Next = null
                }
            });

        static object[] NotEqual<T>(T left, T right) => new object[] { left!, right! };
    }

    private IEqualityComparer<T> GetEqualityComparerUnderTest<T>()
    {
        ITypeShape<T>? shape = Provider.GetShape<T>();
        Assert.NotNull(shape);
        return StructuralEqualityComparer.Create(shape);
    }
}

public class StructuralEqualityTests_Reflection : StructuralEqualityTests
{
    protected override ITypeShapeProvider Provider { get; } = new ReflectionTypeShapeProvider(useReflectionEmit: false);
}

public class StructuralEqualityTests_ReflectionEmit : StructuralEqualityTests
{
    protected override ITypeShapeProvider Provider { get; } = new ReflectionTypeShapeProvider(useReflectionEmit: true);
}

public class StructuralEqualityTests_SourceGen : StructuralEqualityTests
{
    protected override ITypeShapeProvider Provider { get; } = SourceGenTypeShapeProvider.Default;
}