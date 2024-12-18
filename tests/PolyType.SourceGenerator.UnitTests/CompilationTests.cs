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

            #if NET8_0_OR_GREATER
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

#if NET8_0_OR_GREATER
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

            #if NET8_0_OR_GREATER
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
            #if NET8_0_OR_GREATER
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
    
    [Fact]
    public static void TupleTypes_NoErrors()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
           using PolyType;
           using System;

           [GenerateShape<(int, int, int, int, int, int, int, int, int, int)>]
           [GenerateShape<System.Tuple<int, int, int, int, int, int, int, Tuple<int, int, int>>>]
           partial class Witness;
           """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }
    
    [Fact]
    public static void NullableTypes_NoErrors()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
           using PolyType;
           using System;

           [GenerateShape<int?>]
           [GenerateShape<Guid?>]
           partial class Witness;
           """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }
    
    [Fact]
    public static void EnumerableTypes_NoErrors()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
           using PolyType;
           using System;
           using System.Collections.Generic;
           using System.Collections.Immutable;

           [GenerateShape<int[,]>]
           [GenerateShape<ReadOnlyMemory<int>>]
           [GenerateShape<Memory<int>>]
           [GenerateShape<ImmutableArray<int>>]
           [GenerateShape<ImmutableList<int>>]
           [GenerateShape<ImmutableQueue<int>>]
           [GenerateShape<ImmutableStack<int>>]
           [GenerateShape<ImmutableHashSet<int>>]
           [GenerateShape<ImmutableSortedSet<int>>]
           [GenerateShape<IEnumerable<int>>]
           [GenerateShape<ISet<int>>]
           [GenerateShape<List<int>>]
           partial class Witness;
           """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }
    
    [Fact]
    public static void DictionaryTypes_NoErrors()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
           using PolyType;
           using System.Collections;
           using System.Collections.Generic;
           using System.Collections.Immutable;
           using System.Linq;

           [GenerateShape<Dictionary<string, int>>]
           [GenerateShape<ImmutableDictionary<string, int>>]
           [GenerateShape<ImmutableSortedDictionary<string, int>>]
           [GenerateShape<IReadOnlyDictionary<string, int>>]
           [GenerateShape<IDictionary>]
           [GenerateShape<Hashtable>]
           [GenerateShape<CustomDictionary1>]
           partial class Witness;
           
           class CustomDictionary1(IEnumerable<KeyValuePair<string, int>> inner) : Dictionary<string, int>(inner.ToDictionary(kv => kv.Key, kv => kv.Value));
           class CustomDictionary2(Dictionary<string, int> inner) : Dictionary<string, int>(inner);
           """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void PrivateMemberAccessors_NoErrors()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;
            
            #pragma warning disable CS0169
           
            [GenerateShape]
            partial class MyPoco
            {
                [PropertyShape]
                private int PrivateField;
                [PropertyShape]
                private int PrivateProperty { get; set; }
                
                [ConstructorShape]
                private MyPoco(int privateField, int privateProperty)
                {
                    PrivateField = privateField;
                    PrivateProperty = privateProperty;
                }
            }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void DefaultConstructorParameters_NoErrors()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;
            using System;
            #nullable enable

            [GenerateShape]
            public partial record RecordWithNullableDefaultParams2(ulong? x1 = 10, float? x2 = 3.1f, double? x3 = 3.1d, decimal? x4 = -3.1415926m, string? x5 = "str", string? x6 = null, object? x7 = null);
            
            [GenerateShape]
            public partial record RecordWithSpecialValueDefaultParams(
                double d1 = double.PositiveInfinity, double d2 = double.NegativeInfinity, double d3 = double.NaN,
                double? dn1 = double.PositiveInfinity, double? dn2 = double.NegativeInfinity, double? dn3 = double.NaN,
                float f1 = float.PositiveInfinity, float f2 = float.NegativeInfinity, float f3 = float.NaN,
                float? fn1 = float.PositiveInfinity, float? fn2 = float.NegativeInfinity, float? fn3 = float.NaN,
                string s = "\"😀葛🀄\r\n🤯𐐀𐐨\"", char c = '\'');
            
            [Flags]
            public enum MyEnum { A = 1, B = 2, C = 4, D = 8, E = 16, F = 32, G = 64, H = 128 }
            
            [GenerateShape]
            public partial record RecordWithEnumAndNullableParams(MyEnum flags1, MyEnum? flags2, MyEnum flags3 = MyEnum.A, MyEnum? flags4 = null);
            
            [GenerateShape]
            public partial record RecordWithNullableDefaultEnum(MyEnum? flags = MyEnum.A | MyEnum.B);
            
            [GenerateShape]
            public partial record LargeClassRecord(
                int x0 = 0, int x1 = 1, int x2 = 2, int x3 = 3, int x4 = 4, int x5 = 5, int x6 = 5,
                int x7 = 7, int x8 = 8, string x9 = "str", LargeClassRecord? nested = null);                                                                                                                      
            """);
        
        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }
    
    [Fact]
    public static void NullabilityAttributes_NoErrors()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;
            using System.Diagnostics.CodeAnalysis;

            #nullable enable
            
            [GenerateShape]
            public partial class ClassWithNullabilityAttributes
            {
                private string? _maybeNull = "str";
                private string? _allowNull = "str";
                private string? _notNull = "str";
                private string? _disallowNull = "str";
            
                public ClassWithNullabilityAttributes() { }
            
                public ClassWithNullabilityAttributes([AllowNull] string allowNull, [DisallowNull] string? disallowNull)
                {
                    _allowNull = allowNull;
                    _disallowNull = disallowNull;
                }
            
                [MaybeNull]
                public string MaybeNullProperty
                {
                    get => _maybeNull;
                    set => _maybeNull = value;
                }
            
                [AllowNull]
                public string AllowNullProperty
                {
                    get => _allowNull ?? "str";
                    set => _allowNull = value;
                }
            
                [NotNull]
                public string? NotNullProperty
                {
                    get => _notNull ?? "str";
                    set => _notNull = value;
                }
            
                [DisallowNull]
                public string? DisallowNullProperty
                {
                    get => _disallowNull;
                    set => _disallowNull = value;
                }
            
                [MaybeNull]
                public string MaybeNullField = "str";
                [AllowNull]
                public string AllowNullField = "str";
                [NotNull]
                public string? NotNullField = "str";
                [DisallowNull]
                public string? DisallowNullField = "str";
            }                                                                                                         
            """);
        
        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }
}
