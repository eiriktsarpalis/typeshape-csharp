# typeshape-csharp [![Build & Tests](https://github.com/eiriktsarpalis/typeshape-csharp/actions/workflows/build.yml/badge.svg)](https://github.com/eiriktsarpalis/typeshape-csharp/actions/workflows/build.yml) [![NuGet Badge](https://buildstats.info/nuget/typeshape-csharp)](https://www.nuget.org/packages/typeshape-csharp/)

Defines a port of the F# [TypeShape](https://github.com/eiriktsarpalis/TypeShape) library, adapted to patterns and idioms available in C#.

## Motivation

Datatype-generic programs (a.k.a. polytypic programs) refers to components that are capable of acting on the structure of arbitrary types without necessitating any type-specific specialization on behalf of their callers. Common examples include serialization libraries, structured loggers, data mappers, validation libraries, parsers, random generators, equality comparers, and many more.

In System.Text.Json, the method:

```C#
public static class JsonSerializer
{
    public static string Serialize<T>(T value);
}
```

is an example of a datatype-generic program since it accepts values of any type and the schema of the generated JSON is predicated on the shape of the type `T`. Similarly, the method found in Microsoft.Extensions.Configuration:

```C#
public static class ConfigurationBinder
{
    public static T? Get<T>(IConfiguration configuration);
}
```

is a datatype-generic program since the way that configuration is bound is entirely dictated by the shape of `T`.

Authoring datatype-generic programs can be particularly challenging since the code needs to be able to handle _any type_ that the language can declare (including failing gracefully in cases where particular type shapes are not supported). It also needs to be evolved in tandem with new language features as they are being added. 

In the case of C# libraries, here is a non-exhaustive list of language features that a production ready datatype-generic library is expected to handle correctly:

* Constructors, properties, fields and their accessibility modifiers.
* Class inheritance, interface inheritance, virtual members.
* `required`, `readonly` and `init`-only members.
* Collection types, including interface collections, immutable collections, non-generic collections and multi-dimensional arrays.
* Recursive types, e.g. linked list and tree types.
* Record types, structs, ref structs and pointers.
* Special types such as `Nullable<T>` and tuples.

## Introduction

TypeShape is a library that facilitates the development of high-performance datatype-generic programs. It provides:

1. A simplified data model for .NET types that abstracts away concerns of the C# type system. Types can contain properties, have constructors, be collections, but not much else.
2. A [variation on the visitor pattern](https://www.microsoft.com/research/publication/generalized-algebraic-data-types-and-object-oriented-programming/) that enables strongly-typed traversal of arbitrary object graphs, incurring zero allocation cost.
3. Two built-in shape providers that map .NET types to the type model:
    * A [reflection provider](https://github.com/eiriktsarpalis/typeshape-csharp/tree/main/src/TypeShape/ReflectionProvider): uses reflection to derive type models at runtime.
    * A [source generator](https://github.com/eiriktsarpalis/typeshape-csharp/tree/main/src/TypeShape.SourceGenerator): generates type models at compile-time and works with trimmed/Native AOT applications.

In the simplest terms, the library defines a strongly typed reflection model:

```C#
public interface ITypeShape<TDeclaringType> : ITypeShape
{
    IEnumerable<IPropertyShape> GetProperties();
}

public interface IPropertyShape<TDeclaringType, TPropertyType> : IPropertyShape
{
    Func<TDeclaringType, TPropertyType> GetGetter();
    ITypeShape<TPropertyType> PropertyType { get; }
}
```

which can be traversed using generic visitors:

```C#
public interface ITypeShape
{
    object? Accept(ITypeShapeVisitor visitor, object? state);
}

public interface IPropertyShape
{
    object? Accept(ITypeShapeVisitor visitor, object? state);
}

public interface ITypeShapeVisitor
{
    object? Visit<TDeclaringType>(ITypeShape<TDeclaringType> typeShape, object? state);
    object? Visit<TDeclaringType, TPropertyType>(IPropertyShape<TDeclaringType, TPropertyType> typeShape, object? state);
}
```

The shape for any given type can be generated either using the reflection provider:

```C#
using TypeShape.ReflectionProvider;

ITypeShape<MyPoco> shape = ReflectionTypeShapeProvider.Default.GetShape<MyPoco>();

public record MyPoco(string x, string y);
```

or via the source generator:

```C#
ITypeShape<MyPoco> shape = SourceGenProvider.Default.MyPoco;

public record MyPoco(string x, string y);

[GenerateShape(typeof(MyPoco))]
public partial class SourceGenProvider { }
```

Models for types can be fed into datatype-generic consumers that are declared using TypeShape's visitor pattern.

### Example: Writing a datatype-generic counter

The simplest possible example of a datatype-generic programming is counting the number of nodes that exist in a given object graph. This can be implemented by extending the `TypeShapeVisitor` class:

```C#
public sealed partial class CounterVisitor : TypeShapeVisitor
{
    public override object? VisitType<T>(ITypeShape<T> typeShape, object? state)
    {
        // For the sake of simplicity, ignore collection types and just focus on properties/fields.

        // Recursive generate counters for each individual property/field:
        Func<T, int>[] propertyCounters = typeShape.GetProperties(nonPublic: false, includeFields: true)
            .Where(prop => prop.HasGetter)
            .Select(prop => (Func<T, int>)prop.Accept(this, null)!)
            .ToArray();

        // Compose into a counter for the current type.
        return new Func<T, int>(value =>
        {
            if (value is null)
                return 0;

            int count = 1; // the current node itself
            foreach (Func<T, int> propertyCounter in propertyCounters)
                count += propertyCounter(value);

            return count;
        });
    }

    public override object? VisitProperty<TDeclaringType, TPropertyType>(IPropertyShape<TDeclaringType, TPropertyType> propertyShape, object? state)
    {
        Getter<TDeclaringType, TPropertyType> getter = propertyShape.GetGetter(); // extract the getter delegate
        var propertyTypeCounter = (Func<TPropertyType, int>)propertyShape.PropertyType.Accept(this, null)!; // extract the counter for the property type
        return new Func<TDeclaringType, int>(obj => propertyTypeCounter(getter(ref obj))); // compose to a property-specific counter
    }
}
```

We can now define a counter factory using the visitor:

```C#
public static class Counter
{
    private readonly static CounterVisitor s_visitor = new();

    public static Func<T, int> CreateCounter<T>(ITypeShape<T> typeShape)
        => (Func<T, int>)typeShape.Accept(s_visitor, null)!;
}
```

That we can then apply to the shape of our POCO like so:

```C#
ITypeShape<MyPoco> shape = SourceGenProvider.Default.MyPoco;
Func<MyPoco, int> pocoCounter = Counter.CreateCounter(shape);

pocoCounter(new MyPoco("x","y")); // 3

public record MyPoco(string x, string y);

[GenerateShape(typeof(MyPoco))]
public partial class SourceGenProvider { }
```

In essence, TypeShape uses the visitor to fold a strongly typed `Func<MyPoco, int>` counter delegate, but the delegate itself doesn't depend on the visitor once invoked: it only defines a chain of strongly typed delegate invocations that are fast to invoke once constructed.

For more comprehensive examples, please see the [TypeShape.Applications](https://github.com/eiriktsarpalis/typeshape-csharp/tree/main/src/TypeShape.Applications) folder which contains a number of detailed samples. You can use the console apps found in the `tests` folder for more experimentation and exploration.

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

Even though both serializers target the same underlying `JsonConverter` infrastructure, the TypeShape implementation is ~30% faster for serialization and ~90% for deserialization, when compared with System.Text.Json's metadata serializer. As expected, fast-path serialization is still fastest since its implementation is fully inlined.

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

This prototype provides both [compile-time](https://github.com/eiriktsarpalis/typeshape-csharp/tree/main/src/TypeShape.SourceGenerator/Model) and [run-time](https://github.com/eiriktsarpalis/typeshape-csharp/tree/main/src/TypeShape/Abstractions) representations of the model. Even though the examples here primarily focus on the latter approach, there is potential for reusing the compile-time model in authoring bespoke, "fast-path" source generators for datatype-generic programs. The models are lightweight and implement structural equality, meaning they are easy to apply in incremental source generators.
