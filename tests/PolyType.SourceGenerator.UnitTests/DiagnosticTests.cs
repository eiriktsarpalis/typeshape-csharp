using Microsoft.CodeAnalysis;
using Xunit;

namespace PolyType.SourceGenerator.UnitTests;

public static class DiagnosticTests
{
    [Fact]
    public static void GenerateShapeOfT_UnsupportedType_ProducesWarning()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            [GenerateShape<MissingType>]
            public partial class ShapeProvider
            {}
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);

        Diagnostic? diagnostic = result.Diagnostics.FirstOrDefault(d => d.Id == "TS0001");

        Assert.NotNull(diagnostic);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal((2, 1), diagnostic.Location.GetStartPosition());
        Assert.Equal((2, 27), diagnostic.Location.GetEndPosition());
    }

    [Fact]
    public static void GenerateShape_NonPartialClass_ProducesError()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            [GenerateShape]
            public class TypeToGenerate { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);

        Diagnostic diagnostic = Assert.Single(result.Diagnostics);

        Assert.Equal("TS0002", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal((2, 0), diagnostic.Location.GetStartPosition());
        Assert.Equal((3, 31), diagnostic.Location.GetEndPosition());
    }

    [Fact]
    public static void GenerateShapeOfT_NonPartialClass_ProducesError()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            [GenerateShape<TypeToGenerate>]
            public class ShapeProvider { }

            public class TypeToGenerate { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);

        Diagnostic diagnostic = Assert.Single(result.Diagnostics);

        Assert.Equal("TS0002", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal((2, 0), diagnostic.Location.GetStartPosition());
        Assert.Equal((3, 30), diagnostic.Location.GetEndPosition());
    }

    [Fact]
    public static void GenerateShape_GenericType_ProducesWarning()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            [GenerateShape]
            public partial class GenericType<T> 
            {
            }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);

        Diagnostic diagnostic = Assert.Single(result.Diagnostics);

        Assert.Equal("TS0004", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal((2, 0), diagnostic.Location.GetStartPosition());
        Assert.Equal((5, 1), diagnostic.Location.GetEndPosition());
    }

    [Fact]
    public static void GenerateShape_NestedGenericType_ProducesWarning()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            public partial class GenericContainer<T>
            {
                [GenerateShape]
                public partial class TypeToGenerate
                {
                }
            }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);

        Diagnostic diagnostic = Assert.Single(result.Diagnostics);

        Assert.Equal("TS0004", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal((4, 4), diagnostic.Location.GetStartPosition());
        Assert.Equal((7, 5), diagnostic.Location.GetEndPosition());
    }

    [Fact]
    public static void GenerateShapeOfT_GenericType_ProducesWarning()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            [GenerateShape<string>]
            public partial class Witness<T>
            {
            }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);

        Diagnostic diagnostic = Assert.Single(result.Diagnostics);

        Assert.Equal("TS0004", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal((2, 0), diagnostic.Location.GetStartPosition());
        Assert.Equal((5, 1), diagnostic.Location.GetEndPosition());
    }

    [Fact]
    public static void GenerateShapeOfT_NestedGenericType_ProducesWarning()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            public partial class Container<T>
            {
                [GenerateShape<string>]
                public partial class Witness
                {
                }
            }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);

        Diagnostic diagnostic = Assert.Single(result.Diagnostics);

        Assert.Equal("TS0004", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal((4, 4), diagnostic.Location.GetStartPosition());
        Assert.Equal((7, 5), diagnostic.Location.GetEndPosition());
    }

    [Fact]
    public static void GenerateShape_InaccessibleType_ProducesWarning()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            internal static partial class Container
            {
                [GenerateShape]
                private partial record TypeToGenerate(int x);
            }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);

        Diagnostic diagnostic = Assert.Single(result.Diagnostics);

        Assert.Equal("TS0005", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal((4, 4), diagnostic.Location.GetStartPosition());
        Assert.Equal((5, 49), diagnostic.Location.GetEndPosition());
    }
    
    [Fact]
    public static void DuplicateConstructorShapeAttribute_ProducesWarnings()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
           using PolyType;

           [GenerateShape]
           partial class MyPoco
           {
               [ConstructorShape]
               public MyPoco() { }
               
               [ConstructorShape]
               public MyPoco(int value) { }
           }
           """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);

        Diagnostic diagnostic = Assert.Single(result.Diagnostics);

        Assert.Equal("TS0006", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal((9, 11), diagnostic.Location.GetStartPosition());
        Assert.Equal((9, 17), diagnostic.Location.GetEndPosition());
    }
}
