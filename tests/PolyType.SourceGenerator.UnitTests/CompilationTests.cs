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

            #if NET
                public static PolyType.Abstractions.ITypeShape<MyPoco> Test()
                    => PolyType.Abstractions.TypeShapeProvider.Resolve<MyPoco>();
            #endif
            }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
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

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
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

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
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

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
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

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

#if NET
    [Fact]
    public static void UseTypesWithNullableAnnotations_NoWarnings()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;
            using System.Collections.Generic;
            #nullable enable

            public static class Test
            {
                public static void TestMethod()
                {
                    Dictionary<int, string?> dict = new();
                    GenericMethods.TestTypeShapeProvider(dict, new MyProvider());
                }
            }

            public static class GenericMethods
            {
                public static void TestTypeShapeProvider<T, TProvider>(T value, TProvider provider)
                    where TProvider : IShapeable<T> { }
            }

            [GenerateShape<Dictionary<int, string>>]
            public partial class MyProvider { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }
#endif

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

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void MultiplePartialContextDeclarations_NoErrors()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            #if NET
            public static class Test
            {
                public static void TestMethod()
                {
                    PolyType.Abstractions.ITypeShape<string> stringShape = PolyType.Abstractions.TypeShapeProvider.Resolve<string, MyWitness>();
                    PolyType.Abstractions.ITypeShape<int> intShape = PolyType.Abstractions.TypeShapeProvider.Resolve<int, MyWitness>();
                }
            }
            #endif
            
            [GenerateShape<int>]
            public partial class MyWitness;
            
            [GenerateShape<string>]
            public partial class MyWitness;
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
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

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
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

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
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

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void TypeUsingKeywordIdentifiers_GenerateShape_NoErrors()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            [GenerateShape]
            partial class @class
            {
                public @class(string @string, int @__makeref, bool @yield)
                {
                    this.@string = @string;
                    this.@__makeref = @__makeref;
                    this.yield = yield;
                }

                public string @string { get; set; }
                public int @__makeref { get; set; }
                public bool @yield { get; set; }
            }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void NestedWitnessClass_NoErrors()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            internal partial class Outer1
            {
                public partial class Outer2
                {
                    [GenerateShape<MyPoco>]
                    private partial class Witness { }

                    internal record MyPoco(int Value);
                }
            }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Theory]
    [InlineData("partial class")]
    [InlineData("sealed partial class")]
    [InlineData("partial struct")]
    [InlineData("partial record")]
    [InlineData("sealed partial record")]
    [InlineData("partial record struct")]
    public static void SupportedWitnessTypeKinds_NoErrors(string kind)
    {
        Compilation compilation = CompilationHelpers.CreateCompilation($"""
            using PolyType;
            using PolyType.Abstractions;

            ITypeShape<MyPoco> shape;
            #if NET
            shape = TypeShapeProvider.Resolve<MyPoco, Witness>();
            #endif
            shape = TypeShapeProvider.Resolve<MyPoco>(Witness.ShapeProvider);

            record MyPoco(string[] Values);

            [GenerateShape<MyPoco>]
            {kind} Witness;
            """, outputKind: OutputKind.ConsoleApplication);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void ConflictingTypeNames_NoErrors()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            [GenerateShape]
            public partial class MyPoco;

            namespace Foo.Bar
            {
                partial class Container
                {
                    [GenerateShape]
                    public partial class MyPoco;
                }
            }

            namespace Foo.Baz
            {
                partial class Container
                {
                    [GenerateShape]
                    public partial class MyPoco;
                }
            }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void ConflictingTypeNamesInNestedGenerics_NoErrors()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            class Container<T>
            {
                public partial class MyPoco;
            }

            [GenerateShape<Container<int>.MyPoco>]
            [GenerateShape<Container<string>.MyPoco>]
            partial class Witness;
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void TypesUsingReservedIdentifierNames_NoErrors()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            [GenerateShape]
            partial class Default;

            [GenerateShape]
            partial class GetShape;

            [GenerateShape]
            partial class @class;
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void CustomTypeShapeKind_NoErrors()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;
            using PolyType.Abstractions;

            [GenerateShape, TypeShape(Kind = TypeShapeKind.None)]
            public partial record ObjectAsNone(string Name, int Age);
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }
}
