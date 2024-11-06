using System.Collections.Immutable;
using TypeShape.Abstractions;
using TypeShape.Examples.PrettyPrinter;
using TypeShape.ReflectionProvider;
using Xunit;

namespace TypeShape.Tests;

public abstract class PrettyPrinterTests(IProviderUnderTest providerUnderTest)
{
    [Theory]
    [MemberData(nameof(GetValues))]
    public void TestValue<T>(TestCase<T> testCase, string expectedEncoding)
    {
        PrettyPrinter<T> prettyPrinter = GetPrettyPrinterUnderTest(testCase);
        Assert.Equal(expectedEncoding.ReplaceLineEndings(), prettyPrinter.Print(testCase.Value));
    }

    public static IEnumerable<object?[]> GetValues()
    {
        SourceGenProvider p = SourceGenProvider.Default;
        yield return [TestCase.Create(p, 1), "1"];
        yield return [TestCase.Create(p, (string?)null), "null"];
        yield return [TestCase.Create(p, "str"), "\"str\""];
        yield return [TestCase.Create(p, false), "false"];
        yield return [TestCase.Create(p, true), "true"];
        yield return [TestCase.Create(p, (int?)null), "null"];
        yield return [TestCase.Create(p, (int?)42), "42"];
        yield return [TestCase.Create(p, MyEnum.A), "MyEnum.A"];
        yield return [TestCase.Create(p, (int[])[]), "[]"];
        yield return [TestCase.Create(p, (int[])[1, 2, 3]), "[1, 2, 3]"];
        yield return [TestCase.Create(p, (int[][])[[1, 0, 0], [0, 1, 0], [0, 0, 1]]), 
            """
            [
              [1, 0, 0],
              [0, 1, 0],
              [0, 0, 1]
            ]
            """];
        
        yield return [TestCase.Create(p, new object()), "new Object()"];
        yield return [TestCase.Create(new SimpleRecord(42)),
            """
            new SimpleRecord
            {
              value = 42
            }
            """];
        yield return [TestCase.Create(new DerivedClass { X = 1, Y = 2 }),
            """
            new DerivedClass
            {
              Y = 2,
              X = 1
            }
            """];
        yield return [TestCase.Create(p, new Dictionary<string, string>()), "new Dictionary<String, String>()"];
        yield return [TestCase.Create(p,
            new Dictionary<string, string> { ["key"] = "value" }),
            """
            new Dictionary<String, String>
            {
              ["key"] = "value"
            }
            """];
        
        yield return [TestCase.Create(p, ImmutableArray.Create(1,2,3)), """[1, 2, 3]"""];
        yield return [TestCase.Create(p, ImmutableList.Create("1", "2", "3")), """["1", "2", "3"]"""];
        yield return [TestCase.Create(p, ImmutableQueue.Create(1, 2, 3)), """[1, 2, 3]"""];
        yield return [TestCase.Create(p,
            ImmutableDictionary.CreateRange(new Dictionary<string, string> { ["key"] = "value" })),
            """
            new ImmutableDictionary<String, String>
            {
              ["key"] = "value"
            }
            """];

        yield return [TestCase.Create(p,
            ImmutableSortedDictionary.CreateRange(new Dictionary<string, string> { ["key"] = "value" })),
            """
            new ImmutableSortedDictionary<String, String>
            {
              ["key"] = "value"
            }
            """];
    }

    protected PrettyPrinter<T> GetPrettyPrinterUnderTest<T>(TestCase<T> testCase) =>
        PrettyPrinter.Create(providerUnderTest.ResolveShape(testCase));
}

public sealed class PrettyPrinterTests_Reflection() : PrettyPrinterTests(RefectionProviderUnderTest.Default);
public sealed class PrettyPrinterTests_ReflectionEmit() : PrettyPrinterTests(RefectionProviderUnderTest.NoEmit);
public sealed class PrettyPrinterTests_SourceGen() : PrettyPrinterTests(SourceGenProviderUnderTest.Default);