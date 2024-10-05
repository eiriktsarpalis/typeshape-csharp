---
_layout: landing
---

# Introduction

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

1. The [core abstractions](https://eiriktsarpalis.github.io/typeshape-csharp/core-abstractions.html) document for an overview of the core programming model.
2. The [typeshape providers](https://eiriktsarpalis.github.io/typeshape-csharp/typeshape-providers.html) document for an overview of the built-in shape providers and their APIs.
3. The [`TypeShape.Examples`](https://github.com/eiriktsarpalis/typeshape-csharp/tree/main/src/TypeShape.Examples) project for advanced examples of libraries built on top of TypeShape.