# typeshape-csharp [![Build & Tests](https://github.com/eiriktsarpalis/typeshape-csharp/actions/workflows/build.yml/badge.svg)](https://github.com/eiriktsarpalis/typeshape-csharp/actions/workflows/build.yml) [![NuGet Badge](https://img.shields.io/nuget/dt/typeshape-csharp)](https://www.nuget.org/packages/typeshape-csharp/)

`typeshape-csharp` is a practical datatype-generic programming library for .NET types. It is a port of the [TypeShape](https://github.com/eiriktsarpalis/TypeShape) F# library, adapted to patterns and idioms available in C#. See the [project website](https://eiriktsarpalis.github.io/typeshape-csharp) for additional background and [API documentation](https://eiriktsarpalis.github.io/typeshape-csharp/api/TypeShape.html).

## Quick Start

You can try the library by installing the `typeshape-csharp` NuGet package:

```bash
$ dotnet add package typeshape-csharp
```

which includes the core types and source generator for generating type shapes:

```C#
using TypeShape;

[GenerateShape]
public partial record Person(string name, int age);
```

Doing this will augment `Person` with an implementation of the `IShapeable<Person>` interface. This suffices to make `Person` usable with any library that targets the TypeShape core abstractions. You can try this out by installing the built-in example libraries:

```bash
$ dotnet add package TypeShape.Examples
```

Here's how the same value can be serialized to three separate formats.

```csharp
using TypeShape.Examples.JsonSerializer;
using TypeShape.Examples.CborSerializer;
using TypeShape.Examples.XmlSerializer;

Person person = new("Pete", 70);
JsonSerializerTS.Serialize(person); // {"Name":"Pete","Age":70}
XmlSerializer.Serialize(person);    // <value><Name>Pete</Name><Age>70</Age></value>
CborSerializer.EncodeToHex(person); // A2644E616D656450657465634167651846
```

Since the application uses a source generator to produce the shape for `Person`, it is fully compatible with Native AOT. See the [TypeShape providers](https://eiriktsarpalis.github.io/typeshape-csharp/typeshape-providers.html) article for more details on how to use the library with your types.

## Introduction

TypeShape is a meta-library that facilitates rapid development of high performance datatype-generic programs. It exposes a simplified model for .NET types that makes it easy for library authors to publish production-ready components in just a few lines of code. The built-in source generator ensures that any library built on top of the TypeShape abstractions gets Native AOT support for free.

As a library author, TypeShape lets you write high performance, feature complete generic components that target its [core abstractions](https://eiriktsarpalis.github.io/typeshape-csharp/core-abstractions.html). For example, a parser API using TypeShape might look as follows:

```C#
public static class MyFancyParser
{
    public static T? Parse<T>(string myFancyFormat) where T : IShapeable<T>;
}
```

As an end user, TypeShape lets you generate shape models for your own types that can be used with one or more supported libraries:

```C#
Person? person = MyFancyParser.Parse<Person>(format); // Compiles

[GenerateShape] // Generate an IShapeable<TPerson> implementation
partial record Person(string name, int age, List<Person> children);
```

For more information see:

* The [core abstractions](https://eiriktsarpalis.github.io/typeshape-csharp/core-abstractions.html) document for an overview of the core programming model.
* The [typeshape providers](https://eiriktsarpalis.github.io/typeshape-csharp/typeshape-providers.html) document for an overview of the built-in shape providers and their APIs.
* The generated [API documentation](https://eiriktsarpalis.github.io/typeshape-csharp/api/TypeShape.html) for the project.
* The [`TypeShape.Examples`](https://github.com/eiriktsarpalis/typeshape-csharp/tree/main/src/TypeShape.Examples) project for advanced examples of libraries built on top of TypeShape.

## Case Study: Writing a JSON serializer

The repo includes a [JSON serializer](https://github.com/eiriktsarpalis/typeshape-csharp/tree/main/src/TypeShape.Examples/JsonSerializer) built on top of the `Utf8JsonWriter`/`Utf8JsonReader` primitives provided by System.Text.Json. At the time of writing, the full implementation is just under 1200 lines of code but exceeds STJ's built-in `JsonSerializer` both in terms of [supported types](https://github.com/eiriktsarpalis/typeshape-csharp/blob/main/tests/TypeShape.Tests/JsonTests.cs) and performance.

### Performance

Here's a [benchmark](https://github.com/eiriktsarpalis/typeshape-csharp/blob/main/tests/TypeShape.Benchmarks/JsonBenchmark.cs) comparing `System.Text.Json` with the included TypeShape-derived implementation:

#### Serialization

| Method                          | Mean     | Ratio | Allocated | Alloc Ratio |
|-------------------------------- |---------:|------:|----------:|------------:|
| Serialize_StjReflection         | 491.9 ns |  1.00 |     312 B |        1.00 |
| Serialize_StjSourceGen          | 467.0 ns |  0.95 |     312 B |        1.00 |
| Serialize_StjSourceGen_FastPath | 227.2 ns |  0.46 |         - |        0.00 |
| Serialize_TypeShapeReflection   | 277.9 ns |  0.57 |         - |        0.00 |
| Serialize_TypeShapeSourceGen    | 273.6 ns |  0.56 |         - |        0.00 |

#### Deserialization

| Method                          | Mean       | Ratio | Allocated | Alloc Ratio |
|-------------------------------- |-----------:|------:|----------:|------------:|
| Deserialize_StjReflection       | 1,593.0 ns |  1.00 |    1024 B |        1.00 |
| Deserialize_StjSourceGen        | 1,530.3 ns |  0.96 |    1000 B |        0.98 |
| Deserialize_TypeShapeReflection |   773.1 ns |  0.49 |     440 B |        0.43 |
| Deserialize_TypeShapeSourceGen  |   746.7 ns |  0.47 |     440 B |        0.43 |

Even though both serializers target the same underlying reader and writer types, the TypeShape implementation is ~75% faster for serialization and ~100% faster for deserialization, when compared with System.Text.Json's metadata serializer. As expected, fast-path serialization is still fastest since its implementation is fully inlined.

## Known libraries based on TypeShape

The following codebases are based upon TypeShape and may be worth checking out.

- [Nerdbank.MessagePack](https://github.com/AArnott/Nerdbank.MessagePack) - a MessagePack library with performance to rival MessagePack-CSharp, and greater simplicity and additional features.

## Project structure

The repo consists of the following projects:

* The core `TypeShape` library containing:
  * The [core abstractions](https://github.com/eiriktsarpalis/typeshape-csharp/tree/main/src/TypeShape/Abstractions) defining the type model.
  * The [reflection provider](https://github.com/eiriktsarpalis/typeshape-csharp/tree/main/src/TypeShape/ReflectionProvider) implementation.
  * The [model classes](https://github.com/eiriktsarpalis/typeshape-csharp/tree/main/src/TypeShape/SourceGenModel) used by the source generator.
* The [`TypeShape.SourceGenerator`](https://github.com/eiriktsarpalis/typeshape-csharp/tree/main/src/TypeShape.SourceGenerator) project contains the built-in source generator implementation.
* The [`TypeShape.Roslyn`](https://github.com/eiriktsarpalis/typeshape-csharp/tree/main/src/TypeShape.Roslyn) library exposes a set of components for extracting data models from Roslyn type symbols. Used as the foundation for the built-in source generator.
* [`TypeShape.Examples`](https://github.com/eiriktsarpalis/typeshape-csharp/tree/main/src/TypeShape.Examples) containing library examples:
  * A serializer built on top of System.Text.Json,
  * A serializer built on top of System.Xml,
  * A serializer built on top of System.Formats.Cbor,
  * A `ConfigurationBinder` like implementation,
  * A simple pretty-printer for .NET values,
  * A generic random value generator based on `System.Random`,
  * A JSON schema generator for .NET types,
  * An object cloning function,
  * A structural `IEqualityComparer<T>` generator for POCOs and collections,
  * An object validator in the style of System.ComponentModel.DataAnnotations.
  * A simple .NET object mapper.
* The [`applications`](https://github.com/eiriktsarpalis/typeshape-csharp/tree/main/applications) folder contains sample Native AOT console applications.
