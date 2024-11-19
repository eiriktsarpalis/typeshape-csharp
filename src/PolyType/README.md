# PolyType

PolyType is a practical generic programming library for .NET. It facilitates the rapid development of feature-complete, high-performance libraries that interact with user-defined types. This includes serializers, structured loggers, mappers, validators, parsers, random generators, and equality comparers. Its built-in source generator ensures that any library built on top of PolyType gets [Native AOT support for free](https://eiriktsarpalis.wordpress.com/2024/10/22/source-generators-for-free/).

The project is a port of the [TypeShape](https://github.com/eiriktsarpalis/TypeShape) library for F#, adapted to patterns and idioms available in C#. The name PolyType is a reference to [polytypic programming](https://en.wikipedia.org/wiki/Polymorphism_(computer_science)#Polytypism), another term for generic programming.

See the [project website](https://eiriktsarpalis.github.io/PolyType) for additional background and [API documentation](https://eiriktsarpalis.github.io/PolyType/api/PolyType.html).

## Quick Start

You can try the library by installing the `PolyType` NuGet package:

```bash
$ dotnet add package PolyType
```

which includes the core types and source generator for generating type shapes:

```C#
using PolyType;

[GenerateShape]
public partial record Person(string name, int age);
```

Doing this will augment `Person` with an implementation of the `IShapeable<Person>` interface. This suffices to make `Person` usable with any library that targets the PolyType core abstractions. You can try this out by installing the built-in example libraries:

```bash
$ dotnet add package PolyType.Examples
```

Here's how the same value can be serialized to three separate formats.

```csharp
using PolyType.Examples.JsonSerializer;
using PolyType.Examples.CborSerializer;
using PolyType.Examples.XmlSerializer;

Person person = new("Pete", 70);
JsonSerializerTS.Serialize(person); // {"Name":"Pete","Age":70}
XmlSerializer.Serialize(person);    // <value><Name>Pete</Name><Age>70</Age></value>
CborSerializer.EncodeToHex(person); // A2644E616D656450657465634167651846
```

Since the application uses a source generator to produce the shape for `Person`, it is fully compatible with Native AOT. See the [shape providers](https://eiriktsarpalis.github.io/PolyType/shape-providers.html) article for more details on how to use the library with your types.

## Authoring PolyType Libraries

As a library author, PolyType makes it easy to write high-performance, feature-complete components by targeting its [core abstractions](https://eiriktsarpalis.github.io/PolyType/core-abstractions.html). For example, a parser API using PolyType might look as follows:

```C#
public static class MyFancyParser
{
    public static T? Parse<T>(string myFancyFormat) where T : IShapeable<T>;
}
```

The [`IShapeable<T>` constraint](https://eiriktsarpalis.github.io/PolyType/api/PolyType.IShapeable-1.html) indicates that the parser only works with types augmented with PolyType metadata. This metadata can be provided using the PolyType source generator:

```C#
Person? person = MyFancyParser.Parse<Person>(format); // Compiles

[GenerateShape] // Generate an IShapeable<TPerson> implementation
partial record Person(string name, int age, List<Person> children);
```

For more information see:

* The [core abstractions](https://eiriktsarpalis.github.io/PolyType/core-abstractions.html) document for an overview of the core programming model.
* The [shape providers](https://eiriktsarpalis.github.io/PolyType/shape-providers.html) document for an overview of the built-in shape providers and their APIs.
* The generated [API documentation](https://eiriktsarpalis.github.io/PolyType/api/PolyType.html) for the project.
* The [`PolyType.Examples`](https://github.com/eiriktsarpalis/PolyType/tree/main/src/PolyType.Examples) project for advanced examples of libraries built on top of PolyType.
