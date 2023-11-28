using Microsoft.CodeAnalysis;
using Xunit;

namespace TypeShape.SourceGenerator.UnitTests;

public static class CompilationTests
{
    [Fact]
    public static void CompileSimplePoco_NoErrors()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using TypeShape;
            using System.Collections.Generic;

            [GenerateShape]
            public partial class MyPoco
            {
                public MyPoco(bool @bool = true, string @string = "str")
                {
                    Bool = @bool;
                    String = @string;
                }

                public bool Bool { get; }
                public string String { get; }
                public List<int> List { get; set; }
                public Dictionary<string, int> Dict { get; set; }

                public static ITypeShape<MyPoco> Test()
                    => TypeShapeProvider.GetShape<MyPoco>();
            }
            """);

        TypeShapeSourceGeneratorResult result = CompilationHelpers.RunTypeShapeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void UseTypesWithNullableAnnotations_NoWarnings()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using TypeShape;
            using System.Collections.Generic;
            #nullable enable

            public static class Test
            {
                public static void TestMethod()
                {
                    Dictionary<int, string?> dict = new();
                    GenericMethods.TestTypeShape(dict, MyProvider.Default.Dictionary_Int32_String);
                    GenericMethods.TestTypeShapeProvider(dict, MyProvider.Default);
                }
            }

            public static class GenericMethods
            {
                public static void TestTypeShape<T>(T value, ITypeShape<T> shape) { }
                public static void TestTypeShapeProvider<T, TProvider>(T value, TProvider provider)
                    where TProvider : ITypeShapeProvider<T> { }
            }

            [GenerateShape<Dictionary<int, string>>]
            public partial class MyProvider { }
            """);

        TypeShapeSourceGeneratorResult result = CompilationHelpers.RunTypeShapeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }
}
