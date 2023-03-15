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
        Assert.Equal(expected, prettyPrinter.PrettyPrint(value));
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
        yield return GetPair(Array.Empty<int>(), "[]");
        yield return GetPair(new int[] { 1, 2, 3 }, "[1, 2, 3]");
        yield return GetPair(new object(), "{ }");
        yield return GetPair(new SimpleRecord(42), "{ value = 42 }");
        yield return GetPair(new DerivedClass { X = 1, Y = 2 }, "{ Y = 2, X = 1 }");
        yield return GetPair(new Dictionary<string, string>(), "{ }");
        yield return GetPair(new Dictionary<string, string> { ["key"] = "value" }, "{ [\"key\"] = \"value\" }");
        yield return GetPair(new Dictionary<SimpleRecord, string> { [new SimpleRecord(42)] = "value" }, "{ [{ value = 42 }] = \"value\" }");
        
        yield return GetPair(ImmutableArray.Create(1,2,3), "[1, 2, 3]");
        yield return GetPair(ImmutableList.Create("1", "2", "3"), "[\"1\", \"2\", \"3\"]");
        yield return GetPair(ImmutableQueue.Create(1, 2, 3), "[1, 2, 3]");
        yield return GetPair(ImmutableDictionary.CreateRange(new Dictionary<string, string> { ["key"] = "value" }), "{ [\"key\"] = \"value\" }");
        yield return GetPair(ImmutableSortedDictionary.CreateRange(new Dictionary<string, string> { ["key"] = "value" }), "{ [\"key\"] = \"value\" }");

        static object?[] GetPair<T>(T? value, string expected) => new object?[] { value, expected };
    }

    private PrettyPrinter<T> GetPrettyPrinterUnderTest<T>()
    {
        IType<T>? shape = Provider.GetShape<T>();
        Assert.NotNull(shape);
        return PrettyPrinter.CreatePrinter(shape);
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
    protected override ITypeShapeProvider Provider { get; } = SourceGenTypeShapeProvider.Default;
}