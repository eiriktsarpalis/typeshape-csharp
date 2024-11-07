using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace PolyType.SourceGenerator.UnitTests;

public static class CompilationTests
{
    [Fact]
    public static void CompileSimplePoco_NoErrors()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;
            using PolyType.Abstractions;
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
                    => TypeShapeProvider.Resolve<MyPoco>();
            }
            """);

        TypeShapeSourceGeneratorResult result = CompilationHelpers.RunTypeShapeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void CompileSimpleRecord_NoErrors()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            [GenerateShape]
            public partial record MyRecord(string value);
            """);

        TypeShapeSourceGeneratorResult result = CompilationHelpers.RunTypeShapeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void CompileSimpleCollection_NoErrors()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;
            using System.Collections;

            [GenerateShape<ICollection>]
            public partial class MyWitness;
            """);

        TypeShapeSourceGeneratorResult result = CompilationHelpers.RunTypeShapeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void CompileClassWithMultipleSetters_NoErrors()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            [GenerateShape]
            public partial class ClassWithParameterizedConstructorAndMultiplePropertySetters(int x00)
            {
                public int X00 { get; set; } = x00;

                public int X01 { get; set; }
                public int X02 { get; set; }
                public int X03 { get; set; }
                public int X04 { get; set; }
                public int X05 { get; set; }
                public int X06 { get; set; }
                public int X07 { get; set; }
                public int X08 { get; set; }
                public int X09 { get; set; }
                public int X10 { get; set; }
            }
            """);

        TypeShapeSourceGeneratorResult result = CompilationHelpers.RunTypeShapeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void ClassWithSetsRequiredMembersConstructor_NoErrors()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;
            using System.Diagnostics.CodeAnalysis;

            [GenerateShape]
            public partial class MyClass
            {
                [SetsRequiredMembers]
                public MyClass(int value)
                {
                    Value = value;
                }

                public required int Value { get; set; }
            }
            """);

        TypeShapeSourceGeneratorResult result = CompilationHelpers.RunTypeShapeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void UseTypesWithNullableAnnotations_NoWarnings()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;
            using PolyType.Abstractions;
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
                    where TProvider : IShapeable<T> { }
            }

            [GenerateShape<Dictionary<int, string>>]
            public partial class MyProvider { }
            """);

        TypeShapeSourceGeneratorResult result = CompilationHelpers.RunTypeShapeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void DerivedClassWithShadowedMembers_NoErrors()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;
            #nullable enable

            public record BaseClassWithShadowingMembers
            {
                public string? PropA { get; init; }
                public string? PropB { get; init; }
                public int FieldA;
                public int FieldB;
            }

            [GenerateShape]
            public partial record DerivedClassWithShadowingMember : BaseClassWithShadowingMembers
            {
                public new string? PropA { get; init; }
                public required new int PropB { get; init; }
                public new int FieldA;
                public required new string FieldB;
            }
            """);

        TypeShapeSourceGeneratorResult result = CompilationHelpers.RunTypeShapeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void MultiplePartialContextDeclarations_NoErrors()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;
            using PolyType.Abstractions;

            public static class Test
            {
                public static void TestMethod()
                {
                    ITypeShape<string> stringShape = MyWitness.Default.String;
                    ITypeShape<int> intShape = MyWitness.Default.Int32;
                }
            }
            
            [GenerateShape<int>]
            public partial class MyWitness;
            
            [GenerateShape<string>]
            public partial class MyWitness;
            """);

        TypeShapeSourceGeneratorResult result = CompilationHelpers.RunTypeShapeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }
    
    [Fact]
    public static void EnumGeneration_NoErrors()
    {
        // Regression test for https://github.com/eiriktsarpalis/PolyType/issues/29
        Compilation compilation = CompilationHelpers.CreateCompilation("""
           using PolyType;
           
           enum MyEnum { A, B, C }

           [GenerateShape<MyEnum>]
           partial class Witness { }
           """);

        TypeShapeSourceGeneratorResult result = CompilationHelpers.RunTypeShapeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void XmlDocumentGeneration_GenerateShapeOfT_NoErrors()
    {
        // Regression test for https://github.com/eiriktsarpalis/PolyType/issues/35
        CSharpParseOptions parseOptions = CompilationHelpers.CreateParseOptions(documentationMode: DocumentationMode.Diagnose);
        Compilation compilation = CompilationHelpers.CreateCompilation("""
           using PolyType;
           
           /// <summary>My poco.</summary>
           public class MyPoco<T> { }

           /// <summary>My Witness.</summary>
           [GenerateShape<MyPoco<int>>]
           public partial class Witness { }
           """,
           parseOptions: parseOptions);

        TypeShapeSourceGeneratorResult result = CompilationHelpers.RunTypeShapeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void XmlDocumentGeneration_GenerateShape_NoErrors()
    {
        // Regression test for https://github.com/eiriktsarpalis/PolyType/issues/35
        CSharpParseOptions parseOptions = CompilationHelpers.CreateParseOptions(documentationMode: DocumentationMode.Diagnose);
        Compilation compilation = CompilationHelpers.CreateCompilation("""
           using PolyType;
           
           /// <summary>My poco.</summary>
           [GenerateShape]
           public partial class MyPoco { }
           """,
           parseOptions: parseOptions);

        TypeShapeSourceGeneratorResult result = CompilationHelpers.RunTypeShapeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }
}
