# typeshape-csharp [![Build & Tests](https://github.com/eiriktsarpalis/typeshape-csharp/actions/workflows/build.yml/badge.svg)](https://github.com/eiriktsarpalis/typeshape-csharp/actions/workflows/build.yml) [![NuGet Badge](https://buildstats.info/nuget/typeshape-csharp)](https://www.nuget.org/packages/typeshape-csharp/)

This repo contains a port of the [TypeShape](https://github.com/eiriktsarpalis/TypeShape) F# library, adapted to patterns and idioms available in C#.

## Background & Motivation

Datatype-generic programming is the approach to writing components that operate on arbitrary types guided by their own "shape". In this context, the shape or structure of a type refers to the data points that it exposes (fields or properties in objects, elements in collections). Common examples of such programs include serializers, structured loggers, data mappers, validators, parsers, random generators, equality comparers, and many more. In System.Text.Json, the method:

```C#
public static class JsonSerializer
{
    public static string Serialize<T>(T? value);
}
```

is an example of a datatype-generic program because it serializes values of any type, using a schema derived from its shape. Similarly, the method found in Microsoft.Extensions.Configuration:

```C#
public static class ConfigurationBinder
{
    public static T? Get<T>(IConfiguration configuration);
}
```

uses the shape of `T` to derive its configuration binding strategy.

A production ready datatype-generic library should be able to handle _any type_ that the language itself permits (or fail gracefully if it cannot). Anyone who has worked on this space before will know that this is a particularly challenging problem, with full correctness being exceedingly difficult to achieve or verify. For example, authors need to consider things such as:

* Identifying whether a type is an object, collection, enum or something different.
* Resolving the properties or fields that form the data contract of an object.
* Identifying the appropriate construction strategy for a type, if available.
* Addressing inheritance concerns, including virtual properties and diamond ambiguities.
* Supporting special types such as nullable structs, enums, tuples or multi-dimensional arrays.
* Handling recursive types such as linked lists or trees.
* Supporting special modifiers such as `required`, `readonly` and `init`.
* Recognizing members with non-nullable reference annotations.
* Identifying potentially unsupported types such as ref structs, delegates or pointers.

This is far from an exhaustive list, but it serves to highlight the fact that such components are expensive to build and maintain. They need to be kept up-to-date as the framework evolves and the type system accumulates new features.

The goal of this project is to demonstrate that much of this complexity can be consolidated behind a simple set of reusable abstractions that make authoring datatype-generic programs substantially simpler to implement and maintain.

## Introduction

TypeShape is a meta-library that facilitates rapid development of high performance datatype-generic programs. It exposes a simplified model for .NET types that makes it easy for library authors to publish production-ready components in just a few lines of code. The built-in source generator ensures that any library built on top of the TypeShape abstractions gets Native AOT support for free.

As a library author, TypeShape lets you write high performance, feature complete generic components that target its [core abstractions](https://github.com/eiriktsarpalis/typeshape-csharp/tree/main/docs/core-abstractions.md). For example, a parser API using TypeShape might look as follows:

```C#
public static class MyFancyParser
{
    public static T? Parse<T>(string myFancyFormat) where T : ITypeShapeProvider<T>;
}
```

As an end user, TypeShape lets you generate shape models for your own types that can be used with one or more supported libraries:

```C#
Person? person = MyFancyParser.Parse<Person>(format); // Compiles

[GenerateShape] // Generate an ITypeShapeProvider<TPerson> implementation
partial record Person(string name, int age, List<Person> children);
```

For more information see:

1. The [core abstractions](https://github.com/eiriktsarpalis/typeshape-csharp/tree/main/docs/core-abstractions.md) document for an overview of the core programming model.
2. The [typeshape providers](https://github.com/eiriktsarpalis/typeshape-csharp/tree/main/docs/typeshape-providers.md) document for an overview of the built-in shape providers and their APIs.
3. The [`TypeShape.Applications`](https://github.com/eiriktsarpalis/typeshape-csharp/tree/main/src/TypeShape.Applications) project for advanced examples of libraries built on top of TypeShape.

## Case Study: Writing a JSON serializer

The repo includes a [JSON serializer](https://github.com/eiriktsarpalis/typeshape-csharp/tree/main/src/TypeShape.Applications/JsonSerializer) built on top of the `Utf8JsonWriter`/`Utf8JsonReader` primitives provided by System.Text.Json. At the time of writing, the full implementation is just under 1200 lines of code but exceeds STJ's built-in `JsonSerializer` both in terms of [supported types](https://github.com/eiriktsarpalis/typeshape-csharp/blob/main/tests/TypeShape.Tests/JsonTests.cs) and performance.

### Performance

Here's a [benchmark](https://github.com/eiriktsarpalis/typeshape-csharp/blob/main/tests/TypeShape.Benchmarks/JsonBenchmark.cs) comparing `System.Text.Json` with the included TypeShape generator:

#### Serialization

| Method                          | Mean     | Error    | StdDev  | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|-------------------------------- |---------:|---------:|--------:|------:|--------:|-------:|----------:|------------:|
| Serialize_StjReflection         | 557.4 ns | 10.25 ns | 9.59 ns |  1.00 |    0.00 | 0.0019 |     488 B |        1.00 |
| Serialize_StjSourceGen          | 518.7 ns |  4.40 ns | 3.68 ns |  0.93 |    0.02 | 0.0019 |     488 B |        1.00 |
| Serialize_StjSourceGen_FastPath | 310.6 ns |  3.34 ns | 3.12 ns |  0.56 |    0.01 | 0.0005 |     176 B |        0.36 |
| Serialize_TypeShapeReflection   | 399.0 ns |  1.92 ns | 1.70 ns |  0.71 |    0.01 | 0.0005 |     176 B |        0.36 |
| Serialize_TypeShapeSourceGen    | 392.7 ns |  3.39 ns | 3.17 ns |  0.70 |    0.01 | 0.0005 |     176 B |        0.36 |

#### Deserialization

| Method                          | Mean       | Error    | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|-------------------------------- |-----------:|---------:|---------:|------:|--------:|-------:|----------:|------------:|
| Deserialize_StjReflection       | 1,646.6 ns | 22.48 ns | 18.77 ns |  1.00 |    0.00 | 0.0038 |     992 B |        1.00 |
| Deserialize_StjSourceGen        | 1,731.3 ns | 34.00 ns | 40.47 ns |  1.06 |    0.03 | 0.0038 |     968 B |        0.98 |
| Deserialize_TypeShapeReflection |   803.1 ns | 15.86 ns | 21.18 ns |  0.49 |    0.02 | 0.0010 |     440 B |        0.44 |
| Deserialize_TypeShapeSourceGen  |   753.4 ns |  4.93 ns |  3.85 ns |  0.46 |    0.01 | 0.0010 |     440 B |        0.44 |

Even though both serializers target the same underlying reader and writer types, the TypeShape implementation is ~30% faster for serialization and ~90% for deserialization, when compared with System.Text.Json's metadata serializer. As expected, fast-path serialization is still fastest since its implementation is fully inlined.

## Project structure

The repo consists of the following projects:

* The core `TypeShape` library containing:
  * The [core abstractions](https://github.com/eiriktsarpalis/typeshape-csharp/tree/main/src/TypeShape/Abstractions) defining the type model.
  * The [reflection provider](https://github.com/eiriktsarpalis/typeshape-csharp/tree/main/src/TypeShape/ReflectionProvider) implementation.
  * The [model classes](https://github.com/eiriktsarpalis/typeshape-csharp/tree/main/src/TypeShape/SourceGenModel) used by the source generator.
* The [`TypeShape.SourceGenerator`](https://github.com/eiriktsarpalis/typeshape-csharp/tree/main/src/TypeShape.SourceGenerator) project contains the built-in source generator implementation.
* The [`TypeShape.Roslyn`](https://github.com/eiriktsarpalis/typeshape-csharp/tree/main/src/TypeShape.Roslyn) library exposes a set of components for extracting data models from Roslyn type symbols. Used as the foundation for the built-in source generator.
* [`TypeShape.Applications`](https://github.com/eiriktsarpalis/typeshape-csharp/tree/main/src/TypeShape.Applications) containing library examples:
  * A serializer built on top of System.Text.Json,
  * A serializer built on top of System.Xml,
  * A serializer built on top of System.Formats.Cbor,
  * A simple pretty-printer for .NET values,
  * A generic random value generator based on `System.Random`,
  * A JSON schema generator for .NET types,
  * An object cloning function,
  * A structural `IEqualityComparer<T>` generator for POCOs and collections,
  * An object validator in the style of System.ComponentModel.DataAnnotations.
  * A simple .NET object mapper in the style of AutoMapper.
* The [`samples`](https://github.com/eiriktsarpalis/typeshape-csharp/tree/main/samples) folder contains sample Native AOT console applications.
