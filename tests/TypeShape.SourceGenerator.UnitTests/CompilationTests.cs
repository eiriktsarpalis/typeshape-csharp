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
}
