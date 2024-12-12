using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PolyType.Abstractions;
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
            public partial class ShapeProvider;
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
        Assert.Equal((4, 5), diagnostic.Location.GetStartPosition());
        Assert.Equal((4, 18), diagnostic.Location.GetEndPosition());
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

    [Fact]
    public static void GenerateShapeOnStaticClass_ProducesError()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
           using PolyType;

           [GenerateShape]
           static partial class MyClass;
           """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);

        Diagnostic diagnostic = Assert.Single(result.Diagnostics);

        Assert.Equal("TS0007", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal((2, 0), diagnostic.Location.GetStartPosition());
        Assert.Equal((3, 29), diagnostic.Location.GetEndPosition());
    }

    [Fact]
    public static void GenerateShapeOfTOnStaticClass_ProducesError()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
           using PolyType;

           [GenerateShape<MyPoco>]
           static partial class Witness;

           record MyPoco;
           """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);

        Diagnostic diagnostic = Assert.Single(result.Diagnostics);

        Assert.Equal("TS0007", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal((2, 0), diagnostic.Location.GetStartPosition());
        Assert.Equal((3, 29), diagnostic.Location.GetEndPosition());
    }

    [Theory]
    [InlineData(LanguageVersion.CSharp2)]
    [InlineData(LanguageVersion.CSharp7_3)]
    [InlineData(LanguageVersion.CSharp8)]
    [InlineData(LanguageVersion.CSharp10)]
    [InlineData(LanguageVersion.CSharp11)]
    public static void UnsupportedLanguageVersions_ErrorDiagnostic(LanguageVersion langVersion)
    {
        CSharpParseOptions parseOptions = CompilationHelpers.CreateParseOptions(langVersion);
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            [GenerateShape]
            partial class Default
            {
            }
            """, parseOptions: parseOptions);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);

        Diagnostic diagnostic = Assert.Single(result.Diagnostics);

        Assert.Equal("TS0008", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Same(Location.None, diagnostic.Location);
    }

    [Theory]
    [InlineData(LanguageVersion.CSharp12)]
    [InlineData(LanguageVersion.CSharp13)]
    public static void SupportedLanguageVersions_NoErrorDiagnostics(LanguageVersion langVersion)
    {
        CSharpParseOptions parseOptions = CompilationHelpers.CreateParseOptions(langVersion);
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            [GenerateShape]
            partial class Default;
            """, parseOptions: parseOptions);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);
        Assert.Empty(result.Diagnostics);
    }

    [Theory]
    [InlineData(TypeShapeKind.Enum)]
    [InlineData(TypeShapeKind.Nullable)]
    [InlineData(TypeShapeKind.Dictionary)]
    [InlineData(TypeShapeKind.Enumerable)]
    public static void TypeShapeAttribute_ClassWithInvalidKind_ProducesDiagnostic(TypeShapeKind kind)
    {
        Compilation compilation = CompilationHelpers.CreateCompilation($"""
            using PolyType;
            using PolyType.Abstractions;

            [TypeShape(Kind = TypeShapeKind.{kind})]
            class MyPoco;

            [GenerateShape<MyPoco>]
            partial class Witness;
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);
        
        Diagnostic diagnostic = Assert.Single(result.Diagnostics);

        Assert.Equal("TS0009", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal((3, 1), diagnostic.Location.GetStartPosition());
        Assert.Equal(3, diagnostic.Location.GetEndPosition().startLine);
    }

    [Theory]
    [InlineData(TypeShapeKind.Enum)]
    [InlineData(TypeShapeKind.Nullable)]
    public static void TypeShapeAttribute_DictionaryWithInvalidKind_ProducesDiagnostic(TypeShapeKind kind)
    {
        Compilation compilation = CompilationHelpers.CreateCompilation($"""
            using PolyType;
            using PolyType.Abstractions;
            using System.Collections.Generic;

            [TypeShape(Kind = TypeShapeKind.{kind})]
            class MyPoco : Dictionary<string, string>;

            [GenerateShape<MyPoco>]
            partial class Witness;
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);

        Diagnostic diagnostic = Assert.Single(result.Diagnostics);

        Assert.Equal("TS0009", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal((4, 1), diagnostic.Location.GetStartPosition());
        Assert.Equal(4, diagnostic.Location.GetEndPosition().startLine);
    }

    [Theory]
    [InlineData(TypeShapeKind.Enum)]
    [InlineData(TypeShapeKind.Nullable)]
    [InlineData(TypeShapeKind.Dictionary)]
    public static void TypeShapeAttribute_EnumerableWithInvalidKind_ProducesDiagnostic(TypeShapeKind kind)
    {
        Compilation compilation = CompilationHelpers.CreateCompilation($"""
            using PolyType;
            using PolyType.Abstractions;
            using System.Collections.Generic;

            [TypeShape(Kind = TypeShapeKind.{kind})]
            class MyPoco : List<string>;

            [GenerateShape<MyPoco>]
            partial class Witness;
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);

        Diagnostic diagnostic = Assert.Single(result.Diagnostics);

        Assert.Equal("TS0009", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal((4, 1), diagnostic.Location.GetStartPosition());
        Assert.Equal(4, diagnostic.Location.GetEndPosition().startLine);
    }

    [Theory]
    [InlineData(TypeShapeKind.Object)]
    [InlineData(TypeShapeKind.None)]
    public static void TypeShapeAttribute_ClassWithValidKind_NoDiagnostic(TypeShapeKind kind)
    {
        Compilation compilation = CompilationHelpers.CreateCompilation($"""
            using PolyType;
            using PolyType.Abstractions;

            [TypeShape(Kind = TypeShapeKind.{kind})]
            class MyPoco;

            [GenerateShape<MyPoco>]
            partial class Witness;
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);
        
        Assert.Empty(result.Diagnostics);
    }

    [Theory]
    [InlineData(TypeShapeKind.Enumerable)]
    [InlineData(TypeShapeKind.Dictionary)]
    [InlineData(TypeShapeKind.Object)]
    [InlineData(TypeShapeKind.None)]
    public static void TypeShapeAttribute_DictionaryWithValidKind_NoDiagnostic(TypeShapeKind kind)
    {
        Compilation compilation = CompilationHelpers.CreateCompilation($"""
            using PolyType;
            using PolyType.Abstractions;
            using System.Collections.Generic;

            [TypeShape(Kind = TypeShapeKind.{kind})]
            class MyPoco : Dictionary<string, string>;

            [GenerateShape<MyPoco>]
            partial class Witness;
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);

        Assert.Empty(result.Diagnostics);
    }

    [Theory]
    [InlineData(TypeShapeKind.Enumerable)]
    [InlineData(TypeShapeKind.Object)]
    [InlineData(TypeShapeKind.None)]
    public static void TypeShapeAttribute_EnumerableWithValidKind_NoDiagnostic(TypeShapeKind kind)
    {
        Compilation compilation = CompilationHelpers.CreateCompilation($"""
            using PolyType;
            using PolyType.Abstractions;
            using System.Collections.Generic;

            [TypeShape(Kind = TypeShapeKind.{kind})]
            class MyPoco : List<string>;

            [GenerateShape<MyPoco>]
            partial class Witness;
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);

        Assert.Empty(result.Diagnostics);
    }
}
