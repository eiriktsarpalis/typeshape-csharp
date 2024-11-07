# Shape providers

This document provides a walkthrough of the built-in type shape providers. These are typically consumed by end users looking to use their types with libraries built on top of the PolyType core abstractions.

## Source Generator

We can use the built-in source generator to auto-generate shape metadata for a user-defined type like so:

```C#
using PolyType;

[GenerateShape]
partial record Person(string name, int age, List<Person> children);
```

This augments `Person` with an explicit implementation of `IShapeable<Person>`, which can be used an entry point by libraries targeting PolyType:

```C#
MyRandomGenerator.Generate<Person>(); // Compiles

public static class MyRandomGenerator
{
    public static T Generate<T>(int seed = 0) where T : IShapeable<T>;
}
```

The source generator also supports shape generation for third-party types using witness types:

```C#
[GenerateShape<Person[]>]
[GenerateShape<List<int>>]
public partial class Witness; // : IShapeable<Person[]>, IShapeable<List<int>>
```

which can be applied against supported libraries like so:

```C#
MyRandomGenerator.Generate<Person[], Witness>() // Compiles
MyRandomGenerator.Generate<List<int>, Witness>() // Compiles

public static class MyRandomGenerator
{
    public static T Generate<T, TWitness>(int seed = 0) where TWitness : IShapeable<T>;
}
```

## Reflection Provider

PolyType includes a reflection-based provider that resolves shape metadata at run time:

```C#
using PolyType.ReflectionProvider;

ITypeShapeProvider provider = ReflectionTypeShapeProvider.Default;
var shape = (ITypeShape<Person>)provider.GetShape(typeof(Person));
```

Which can be consumed by supported libraries as follows:

```C#
MyRandomGenerator.Generate<Person>(ReflectionTypeShapeProvider.Default);
MyRandomGenerator.Generate<Person[][]>(ReflectionTypeShapeProvider.Default);
MyRandomGenerator.Generate<List<int>>(ReflectionTypeShapeProvider.Default);

public static class MyRandomGenerator
{
    public static T Generate<T>(ITypeShapeProvider provider);
}
```

By default, the reflection provider uses dynamic methods (Reflection.Emit) to speed up reflection, however this might not be desirable when running in certain platforms (e.g. blazor-wasm). It can be turned off using the relevant constructor parameter:

```C#
ITypeShapeProvider provider = new ReflectionTypeShapeProvider(useReflectionEmit: false);
```

## Shape attributes

PolyType exposes a number of attributes that tweak aspects of the generated shape. These attributes are recognized both by the source generator and the reflection provider.

### PropertyShapeAttribute

Configures aspects of a generated property shape, for example:

```C#
class UserData
{
    [PropertyShape(Name = "id", Order = 0)]
    public required string Id { get; init; }

    [PropertyShape(Name = "name", Order = 1)]
    public string? Name { get; init; }

    [PropertyShape(Ignore = true)]
    public string? UserSecret { get; init; }
}
```

Compare with `System.Runtime.Serialization.DataMemberAttribute` and `Newtonsoft.Json.JsonPropertyAttribute`.

### ConstructorShapeAttribute

Can be used to pick a specific constructor for a given type, if there is ambiguity:

```C#
class PocoWithConstructors
{
    public PocoWithConstructors();
    [ConstructorShape] // <--- Only use this constructor in PolyType apps
    public PocoWithConstructors(int x1, int x2);
}
```

Compare with `System.Text.Json.Serialization.JsonConstructorAttribute`.

### ParameterShapeAttribute

Configures aspects of a constructor parameter shape:

```C#
class PocoWithConstructors
{
    public PocoWithConstructors([ParameterShape(Name = "name")] string x1, [ParameterShape(Name = "age")] int x2);
}
```
