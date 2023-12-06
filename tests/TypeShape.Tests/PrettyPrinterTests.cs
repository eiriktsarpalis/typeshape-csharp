using System.Collections.Immutable;
using TypeShape.Applications.PrettyPrinter;
using TypeShape.ReflectionProvider;
using Xunit;

namespace TypeShape.Tests;

public abstract class PrettyPrinterTests
{
    protected abstract ITypeShapeProvider Provider { get; }

    [Theory]
    [MemberData(nameof(GetValues))]
    public void TestValue<T>(T value, string expected)
    {
        PrettyPrinter<T> prettyPrinter = GetPrettyPrinterUnderTest<T>();
        Assert.Equal(expected, prettyPrinter.Print(value));
    }

    public static IEnumerable<object?[]> GetValues()
    {
        yield return GetPair(1, "1");
        yield return GetPair((string?)null, "null");
        yield return GetPair("str", "\"str\"");
        yield return GetPair(false, "false");
        yield return GetPair(true, "true");
        yield return GetPair((int?)null, "null");
        yield return GetPair((int?)42, "42");
        yield return GetPair(MyEnum.A, "\"A\"");
        yield return GetPair<int[]>([], "[]");
        yield return GetPair<int[]>([1, 2, 3], "[1, 2, 3]");
        yield return GetPair<int[][]>([[1, 0, 0], [0, 1, 0], [0, 0, 1]], 
            """
            [
              [1, 0, 0],
              [0, 1, 0],
              [0, 0, 1]
            ]
            """);
        yield return GetPair(new object(), "new Object()");
        yield return GetPair(new SimpleRecord(42), 
            """
            new SimpleRecord
            {
              value = 42
            }
            """);
        yield return GetPair(new DerivedClass { X = 1, Y = 2 }, 
            """
            new DerivedClass
            {
              Y = 2,
              X = 1
            }
            """);
        yield return GetPair(new Dictionary<string, string>(), "new Dictionary`2()");
        yield return GetPair(
            new Dictionary<string, string> { ["key"] = "value" }, 
            """
            new Dictionary`2
            {
              ["key"] = "value"
            }
            """);
        
        yield return GetPair(ImmutableArray.Create(1,2,3), """[1, 2, 3]""");
        yield return GetPair(ImmutableList.Create("1", "2", "3"), """["1", "2", "3"]""");
        yield return GetPair(ImmutableQueue.Create(1, 2, 3), """[1, 2, 3]""");
        yield return GetPair(
            ImmutableDictionary.CreateRange(new Dictionary<string, string> { ["key"] = "value" }),
            """
            new ImmutableDictionary`2
            {
              ["key"] = "value"
            }
            """);

        yield return GetPair(
            ImmutableSortedDictionary.CreateRange(new Dictionary<string, string> { ["key"] = "value" }), 
            """
            new ImmutableSortedDictionary`2
            {
              ["key"] = "value"
            }
            """);

        static object?[] GetPair<T>(T? value, string expected) => [value, expected.ReplaceLineEndings()];
    }

    private PrettyPrinter<T> GetPrettyPrinterUnderTest<T>()
    {
        ITypeShape<T>? shape = Provider.GetShape<T>();
        Assert.NotNull(shape);
        return PrettyPrinter.Create(shape);
    }
}

public class PrettyPrinterTests_Reflection : PrettyPrinterTests
{
    protected override ITypeShapeProvider Provider { get; } = new ReflectionTypeShapeProvider(useReflectionEmit: false);
}

public class PrettyPrinterTests_ReflectionEmit : PrettyPrinterTests
{
    protected override ITypeShapeProvider Provider { get; } = new ReflectionTypeShapeProvider(useReflectionEmit: true);
}

public class PrettyPrinterTests_SourceGen : PrettyPrinterTests
{
    [Theory]
    [MemberData(nameof(TestTypes.GetTestCases), MemberType = typeof(TestTypes))]
    public void TypeShapeProvider_ReturnsExpectedResult<T, TProvider>(TestCase<T, TProvider> value) where TProvider : ITypeShapeProvider<T>
    {
        string expectedResult = PrettyPrinter.Create(SourceGenProvider.Default.GetShape<T>()!).Print(value.Value);

        string result = PrettyPrinter.Print<T, TProvider>(value.Value);

        Assert.Equal(expectedResult, result);
    }

    protected override ITypeShapeProvider Provider { get; } = SourceGenProvider.Default;
}