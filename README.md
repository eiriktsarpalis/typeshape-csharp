# typeshape-csharp [![Build & Tests](https://github.com/eiriktsarpalis/typeshape-csharp/actions/workflows/build.yml/badge.svg)](https://github.com/eiriktsarpalis/typeshape-csharp/actions/workflows/build.yml) [![NuGet Badge](https://buildstats.info/nuget/typeshape-csharp)](https://www.nuget.org/packages/typeshape-csharp/)

Contains a proof-of-concept port of the [TypeShape](https://github.com/eiriktsarpalis/TypeShape) library, adapted to patterns and idioms available in C#.
The library provides a .NET datatype model that facilitates developing high-performance datatype-generic components such as serializers, loggers, transformers and validators.
At its core, the programming model employs a [variation on the visitor pattern](https://www.microsoft.com/en-us/research/publication/generalized-algebraic-data-types-and-object-oriented-programming/) that enables strongly-typed traversal of arbitrary type graphs: it can be used to generate object traversal algorithms that incur zero allocation cost.

The project includes two typeshape model providers: one [reflection-derived](https://github.com/eiriktsarpalis/typeshape-csharp/tree/main/src/TypeShape/ReflectionProvider) and one [source generated](https://github.com/eiriktsarpalis/typeshape-csharp/tree/main/src/TypeShape.SourceGenerator).
It follows that any datatype-generic application built on top of the typeshape model gets trim safety/NativeAOT support for free once it targets source generated models.

## Project structure

The repo consists of the following projects:

* The core `TypeShape` library containing:
  * The [set of abstractions](https://github.com/eiriktsarpalis/typeshape-csharp/tree/main/src/TypeShape/Abstractions) defining the type model.
  * A [reflection provider](https://github.com/eiriktsarpalis/typeshape-csharp/tree/main/src/TypeShape/ReflectionProvider) implementation.
  * The [model classes](https://github.com/eiriktsarpalis/typeshape-csharp/tree/main/src/TypeShape/SourceGenModel) used by the source generator.
* The [`TypeShape.SourceGenerator`](https://github.com/eiriktsarpalis/typeshape-csharp/tree/main/src/TypeShape.SourceGenerator) implementation.
  * Defines an incremental source generator following a [triggering model](https://github.com/eiriktsarpalis/typeshape-csharp/blob/7f517209fd34cf80b5ba83b21306c6e8bf836ae9/tests/TypeShape.SourceGenApp/Program.cs#L38-L41) identical to `System.Text.Json`.
* [`TypeShape.Applications`](https://github.com/eiriktsarpalis/typeshape-csharp/tree/main/src/TypeShape.Applications) containing generic program examples:
  * A serializer generator targeting System.Text.Json's `JsonConverter`,
  * A simple pretty-printer for .NET values,
  * A generic random value generator based on `System.Random`,
  * A structural `IEqualityComparer<T>` generator for POCOs and collections,
  * An object validator in the style of System.ComponentModel.DataAnnotations.
  * A serializer built on top of System.Formats.Cbor.
* [`TypeShape.ReflectionApp`](https://github.com/eiriktsarpalis/typeshape-csharp/tree/main/tests/TypeShape.ReflectionApp) and [`TypeShape.SourceGenApp`](https://github.com/eiriktsarpalis/typeshape-csharp/tree/main/tests/TypeShape.SourceGenApp) define simple console apps for testing generic programs in CoreCLR and NativeAOT.

## Getting Started

For a quick end-to-end overview of how the programming model works, I would recommend looking at the [PrettyPrinter](https://github.com/eiriktsarpalis/typeshape-csharp/tree/main/src/TypeShape.Applications/PrettyPrinter) example. At just below 300 loc, it is the simplest component defined in the repo. You can use the console apps found in the `tests` folder to experiment with the implementation.

## Case Study: Writing a JSON serializer

The repo includes a [JSON serializer](https://github.com/eiriktsarpalis/typeshape-csharp/tree/main/src/TypeShape.Applications/JsonSerializer) example that uses TypeShape to generate System.Text.Json `JsonConverter<T>` instances for arbitrary .NET data types. The implementation offers [functional parity](https://github.com/eiriktsarpalis/typeshape-csharp/blob/main/tests/TypeShape.Tests/JsonTests.cs) with STJ in supported types.

### Performance

Here's a [benchmark](https://github.com/eiriktsarpalis/typeshape-csharp/blob/main/tests/TypeShape.Benchmarks/JsonBenchmark.cs) comparing `System.Text.Json` with the included TypeShape generator:

#### Serialization

|                          Method |     Mean |    Error |   StdDev | Ratio | RatioSD |   Gen0 | Allocated | Alloc Ratio |
|-------------------------------- |---------:|---------:|---------:|------:|--------:|-------:|----------:|------------:|
|         Serialize_StjReflection | 567.0 ns | 11.10 ns | 12.78 ns |  1.00 |    0.00 | 0.0381 |     488 B |        1.00 |
|          Serialize_StjSourceGen | 571.5 ns | 10.80 ns | 11.09 ns |  1.01 |    0.04 | 0.0381 |     488 B |        1.00 |
| Serialize_StjSourceGen_FastPath | 293.3 ns |  5.54 ns |  6.16 ns |  0.52 |    0.02 | 0.0138 |     176 B |        0.36 |
|   Serialize_TypeShapeReflection | 412.0 ns |  8.01 ns | 10.42 ns |  0.73 |    0.03 | 0.0138 |     176 B |        0.36 |
|    Serialize_TypeShapeSourceGen | 402.2 ns |  8.01 ns |  9.23 ns |  0.71 |    0.02 | 0.0138 |     176 B |        0.36 |

#### Deserialization

|                          Method |       Mean |    Error |   StdDev | Ratio | RatioSD |   Gen0 | Allocated | Alloc Ratio |
|-------------------------------- |-----------:|---------:|---------:|------:|--------:|-------:|----------:|------------:|
|       Deserialize_StjReflection | 1,589.5 ns | 31.56 ns | 68.60 ns |  1.00 |    0.00 | 0.0782 |     992 B |        1.00 |
|        Deserialize_StjSourceGen | 1,574.9 ns | 24.86 ns | 19.41 ns |  0.96 |    0.04 | 0.0763 |     968 B |        0.98 |
| Deserialize_TypeShapeReflection |   848.8 ns | 16.65 ns | 23.88 ns |  0.52 |    0.02 | 0.0343 |     440 B |        0.44 |
|  Deserialize_TypeShapeSourceGen |   828.6 ns | 16.04 ns | 15.76 ns |  0.50 |    0.01 | 0.0343 |     440 B |        0.44 |

Even though both serializers target the same underlying `JsonConverter` infrastructure, the TypeShape implementation is ~30% faster for serialization and ~90% for deserialization,
when compared with System.Text.Json's metadata serializer. As expected, fast-path serialization is still fastest since its implementation is fully inlined.

## Advantages & Disadvantages

The library has a number of advantages:

1. Low barrier of entry when porting to NativeAOT: you get a source generator for free so don't need to write your own.
2. The same source generator/type model can be used reused across multiple components, in theory meaning reduced compile times and static footprints.
3. Reusable/consistent handling of the .NET/C# type system across components: struct vs class semantics, inheritance, virtual properties, init & required properties, etc.

At the same time the approach also has a number of drawbacks, namely:

1. Mapping the type model to any particular datatype-generic component needs to happen at runtime, which incurs one-off startup costs.
2. The type model is defined using generic interfaces, which should have impact on the static footprint of a trimmed application.
3. Whether using reflection or source gen, member access is driven using delegates is slower than direct access in inlined, "fast-path" serializers.
4. At present there is no mechanism for filtering generated member accessors at compile time.

## Next Steps

This experiment is an exploration of potential reusable source generator abstractions when applied to datatype-generic programming. It proposes to do so by reducing the rich models found in `ITypeSymbol` (or `System.Type` in the case of runtime reflection) into a simplified, data-oriented model for types. This intermediate representation abstracts away runtime/language concerns such as classes vs. structs, fields vs. properties, implementation and interface inheritance, accessibility modifiers, required and init-only properties, etc.

Under such a data-oriented representation a type consists of:

1. A list of accessible instance properties.
2. A list of accessible constructors plus relevant parameter metadata.
3. Additional metadata for special kinds of data types, such as
    * Element type for enumerable-like types.
    * Key and Value type for dictionary-like types.
    * Element type for `Nullable<T>`.
    * Underlying type for enums.
4. Attribute metadata for all the nodes described above.

This prototype provides both [compile-time](https://github.com/eiriktsarpalis/typeshape-csharp/tree/main/src/TypeShape.SourceGenerator/Model) and [run-time](https://github.com/eiriktsarpalis/typeshape-csharp/tree/main/src/TypeShape/Abstractions) representations of the model. Even though the examples here primarily focus on the latter approach, there is potential for reusing the compile-time model in authoring bespoke, "fast-path" source generators for datatype-generic programs. The models are lightweight and implement structural equality, meaning they are easy to apply in incremental source generators.
