# Core Abstractions

This document provides a walkthrough the core type abstractions found in TypeShape. This includes `ITypeShape`, `IPropertyShape` and the visitor types for accessing them. These are typically consumed by library authors looking to build datatype-generic components. Unless otherwise stated, all APIs are found in the `TypeShape.Abstractions` namespace.

## The `ITypeShape` interface

The `ITypeShape` interface defines a reflection-like representation for a given .NET type. The type hierarchy that it creates encapsulates all information necessary to perform strongly typed traversal of its type graph.

To illustrate the idea, consider the following APIs modelling objects with properties:

```C#
namespace TypeShape.Abstractions;

public partial interface IObjectTypeShape<TDeclaringType> : ITypeShape
{
    IEnumerable<IPropertyShape> GetProperties();
}

public partial interface IPropertyShape<TDeclaringType, TPropertyType> : IPropertyShape
{
    IObjectTypeShape<TPropertyType> PropertyType { get; }
    Func<TDeclaringType, TPropertyType> GetGetter();
    bool HasGetter { get; }
}
```

This model is fairly similar to `System.Type` and `System.Reflection.PropertyInfo`, with the notable difference that both models are generic and the property shape is capable of producing a strongly typed getter delegate. It can be traversed using the following generic visitor type:

```C#
public partial interface ITypeShapeVisitor
{
    object? VisitObject<TDeclaringType>(IObjectTypeShape<TDeclaringType> objectShape, object? state = null);
    object? VisitProperty<TDeclaringType, TPropertyType>(IPropertyShape<TDeclaringType, TPropertyType> typeShape, object? state = null);
}

public partial interface ITypeShape
{
    object? Accept(ITypeShapeVisitor visitor, object? state = null);
}

public partial interface IPropertyShape
{
    object? Accept(ITypeShapeVisitor visitor, object? state = null);
}
```

Here's a simple visitor used to construct delegates counting the number nodes in an object graph:

```C#
partial class CounterVisitor : TypeShapeVisitor
{
    public override object? VisitObject<T>(IObjectTypeShape<T> objectShape, object? _)
    {
        // Generate counter delegates for each individual property or field:
        Func<T, int>[] propertyCounters = objectShape.GetProperties()
            .Where(prop => prop.HasGetter)
            .Select(prop => (Func<T, int>)prop.Accept(this)!)
            .ToArray();

        // Compose into a counter delegate for the current type.
        return new Func<T?, int>(value =>
        {
            if (value is null)
                return 0;

            int count = 1;
            foreach (Func<T, int> propertyCounter in propertyCounters)
                count += propertyCounter(value);

            return count;
        });
    }

    public override object? VisitProperty<TDeclaringType, TPropertyType>(IPropertyShape<TDeclaringType, TPropertyType> propertyShape, object? _)
    {
        Getter<TDeclaringType, TPropertyType> getter = propertyShape.GetGetter(); // extract the getter delegate
        var propertyTypeCounter = (Func<TPropertyType, int>)propertyShape.PropertyType.Accept(this)!; // extract the counter for the property shape
        return new Func<TDeclaringType, int>(obj => propertyTypeCounter(getter(ref obj))); // combine into a property-specific counter delegate
    }
}
```

Given an `ITypeShape<T>` instance we can now construct a counter delegate like so:

```C#
ITypeShape<MyPoco> shape = provider.GetShape<MyPoco>();
CounterVisitor visitor = new();
var pocoCounter = (Func<MyPoco, int>)shape.Accept(visitor)!;

pocoCounter(new MyPoco("x", "y")); // 3
pocoCounter(new MyPoco("x", null)); // 2
pocoCounter(new MyPoco(null, null)); // 1
pocoCounter(null); // 0

record MyPoco(string? x, string? y);
```

It should be noted that the visitor is only used when constructing, or _folding_ the counter delegate but not when the delegate itself is being invoked. At the same time, traversing the type graph via the visitor requires casting of the intermediate delegates, however the traversal of the object graph via the resultant delegate is fully type-safe and doesn't require any casting.

> [!NOTE]
> In technical terms, `ITypeShape` encodes a [GADT representation](https://en.wikipedia.org/wiki/Generalized_algebraic_data_type) over .NET types and `ITypeShapeVisitor` encodes a pattern match over the GADT. This technique was originally described in [this publication](https://www.microsoft.com/research/publication/generalized-algebraic-data-types-and-object-oriented-programming/).
>
> The casting requirement for visitors is a known restriction of this approach, and possible extensions to the C# type system that allow type-safe pattern matching on GADTs are discussed in the paper.

### Collection type shapes

A collection type in this context refers to any type implementing `IEnumerable`, and this is further refined into enumerable and dictionary shapes:

```C#
public interface IEnumerableShape<TEnumerable, TElement> : ITypeShape<TEnumerable>
{
    ITypeShape<TElement> ElementType { get; }

    Func<TEnumerable, IEnumerable<TElement>> GetGetEnumerable();
}

public interface IDictionaryShape<TDictionary, TKey, TValue> : ITypeShape<TDictionary>
{
    ITypeShape<TKey> KeyType { get; }
    ITypeShape<TValue> ValueType { get; }

    Func<IReadOnlyDictionary<TKey, TValue>> GetGetDictionary();
}
```

A collection type is classed as a dictionary if it implements one of the known dictionary interfaces. Non-generic collections use `object` as the element, key and value types. As before, enumerable shapes can be unpacked by the relevant methods of `ITypeShapeVisitor`:

```C#
public interface ITypeShapeVisitor
{
    object? VisitEnumerable<TEnumerable, TElement>(IEnumerableShape<TEnumerable, TElement> enumerableShape, object? state = null);
    object? VisitDictionary<TDictionary, TKey, TValue>(IDictionaryShape<TDictionary, TKey, TValue> dictionaryShape, object? state = null);
}
```

Using the above we can now extend `CounterVisitor` so that collection types are supported:

```C#
partial class CounterVisitor : TypeShapeVisitor
{
    public override object? VisitEnumerable<TEnumerable, TElement>(IEnumerableShape<TEnumerable, TElement> enumerableShape, object? _)
    {
        var elementCounter = (Func<TElement, int>)enumerableShape.ElementType.Accept(this)!;
        Func<TEnumerable, IEnumerable<TElement>> getEnumerable = enumerableShape.GetGetEnumerable();
        return new Func<TEnumerable, int>(enumerable =>
        {
            if (enumerable is null) return 0;
            
            int count = 0;
            foreach (TElement element in getEnumerable(enumerable))
                count += elementCounter(element);

            return count;
        });
    }

    public override object? VisitDictionary<TDictionary, TKey, TValue>(IDictionaryShape<TDictionary, TKey, TValue> dictionaryShape, object? _)
    {
        var keyCounter = (Func<TKey, int>)dictionaryShape.KeyType.Accept(this);
        var valueCounter = (Func<TValue, int>)dictionaryShape.ValueType.Accept(this);
        Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> getDictionary = dictionaryShape.GetGetDictionary();
        return new Func<TDictionary, int>(dictionary =>
        {
            if (dictionary is null) return 0;
            
            int count = 0;
            foreach (var kvp in getDictionary(dictionary))
            {
                count += keyCounter(kvp.Key);
                count += valueCounter(kvp.Value);
            }

            return count;
        });
    }
}
```

### Enum types and `Nullable<T>`

Enum types and `Nullable<T>` as classed as special type shapes:

```C#
public interface IEnumTypeShape<TEnum, TUnderlying> : ITypeShape<TEnum> where TEnum : struct, Enum
{
    public ITypeShape<TUnderlying> UnderlyingType { get; }
}

public interface INullableTypeShape<TElement> : ITypeShape<TElement?> where TElement : struct
{
    public ITypeShape<TElement> ElementType { get; }
}
```

The `TUnderlying` represents the underlying numeric representation used by the enum in question. As before, `ITypeShapeVisitor` exposes relevant methods for consuming the new shapes:

```C#
public interface ITypeShapeVisitor
{
    object? VisitEnum<TEnum, TUnderlying>(IEnumTypeShape<TEnum, TUnderlying> enumShape, object? state = null) where TEnum : struct, Enum;
    object? VisitNullable<TElement>(INullableTypeShape<TElement> nullableShape, object? state = null) where TElement : struct;
}
```

Like before we can extend `CounterVisitor` to nullable and enum types like so:

```C#
partial class CounterVisitor : TypeShapeVisitor
{
    public override object? VisitEnum<TEnum, TUnderlying>(IEnumTypeShape<TEnum, TUnderlying> _, object? _)
    {
        return new Func<TEnum, int>(_ => 1);
    }

    public override object? VisitNullable<TElement>(INullableTypeShape<TElement> nullableShape, object? _)
    {
        var elementCounter = (Func<TElement, int>)nullableShape.ElementType.Accept(this)!;
        return new Func<TElement?, int>(nullable => nullable is null ? 0 : elementConverter(nullable.Value));
    }
}
```

To recap, the `ITypeShape` model splits .NET types into five separate kinds:

* Base `ITypeShape` instances which may or may not define properties,
* `IEnumerableShape` instances describing enumerable types,
* `IDictionaryShape` instances describing dictionary types,
* `IEnumShape` instances describing enum types and
* `INullableShape` instances describing `Nullable<T>` types.

## Constructing and mutating types

The APIs described so far facilitate algorithms that perform object traversal such as serializers, formatters and validators. They do not suffice when it comes to writing algorithms that perform object construction or mutation such as deserializers, mappers and random value generators. This section describes the constructs used for writing this class of algorithms.

### Property setters

The `IPropertyShape` interface exposes strongly typed setter delegates:

```C#
public interface IPropertyShape<TDeclaringType, TPropertyType>
{
    Setter<TDeclaringType, TPropertyType> GetSetter();
    bool HasSetter { get; }
}

public delegate void Setter<TDeclaringType, TPropertyType>(ref TDeclaringType obj, TPropertyType value);
```

The setter is defined using a special delegate the accepts the declaring type by reference, ensuring that it has the expected behavior when working with value types. To illustrate how this works, here is a toy example that sets all properties to their default value:

```C#
public delegate void Mutator<T>(ref T obj);

class MutatorVisitor : TypeShapeVisitor
{
    public override object? VisitObject(IObjectTypeShape<T> objectShape, object? _)
    {
        Mutator<T>[] propertyMutators = objectShape.GetProperties()
            .Where(prop => prop.HasSetter)
            .Select(prop => (Mutator<T>)prop.Accept(this)!)
            .ToArray();

        return new Mutator<T>(ref T value => foreach (var mutator in propertyMutators) mutator(ref value));
    }

    public override object? VisitProperty<TDeclaringType, TPropertyType>(IPropertyShape<TDeclaringType, TPropertyType> propertyShape, object? _)
    {
        Setter<TDeclaringType, TPropertyType> setter = propertyShape.GetSetter();
        return new Mutator<TDeclaringType>(ref TDeclaringType obj => setter(ref obj, default(TPropertyType)!));
    }
}
```

which can be consumed as follows:

```C#
ITypeShape<MyPoco> shape = provider.GetShape<MyPoco>();
MutatorVisitor visitor = new();
var mutator = (Mutator<MyPoco>)shape.Accept(visitor)!;

var value = new MyPoco { X = "X" };
mutator(ref value);
Console.WriteLine(value); // MyPoco { X =  }

struct MyPoco
{
    public string? X { get; set; }
}
```

### Constructor shapes

While property setters should suffice when mutating existing objects, constructing a new instance from scratch is somewhat more complicated, particularly for types that only expose parameterized constructors or are immutable. TypeShape models constructors using the `IConstructorShape` abstraction which can be obtained as follows:

```C#
public partial interface IObjectTypeShape<T>
{
    IConstructorShape? GetConstructor();
}

public partial interface IConstructorShape<TDeclaringType, TArgumentState> : IConstructorShape
{
    int ParameterCount { get; }
    Func<TArgumentState> GetArgumentStateConstructor();
    Func<TArgumentState, TDeclaringType> GetParameterizedConstructor();
}

public partial interface ITypeShapeVisitor
{
    object? VisitConstructor<TDeclaringType, TArgumentState>(IConstructorShape<TDeclaringType, TArgumentState> shape, object? state = null);
}
```

The constructor shape specifies two type parameters: `TDeclaringType` represents the declaring type of the constructor while `TArgumentState` represents an opaque, mutable token that encapsulates all parameters that will be passed to constructor. The choice of `TArgumentState` is up to the particular type shape provider implementation, but typically a value tuple is used:

```C#
record MyPoco(int x = 42, string y);

class MyPocoConstructorShape : IConstructorShape<MyPoco, (int, string)>
{
    public int ParameterCount => 2;
    public Func<(int, string)> GetArgumentStateConstructor() => () => (42, null!);
    public Func<(int, string), MyPoco> GetParameterizedConstructor() => state => new MyPoco(state.Item1, state.Item2);
}
```

The two delegates define the means for creating a default instance of the mutable state token and constructing an instance of the declaring type from a populated token, respectively. Separately, there needs to be a mechanism for populating the state token which is achieved using the `IConstructorParameterShape` interface:

```C#
public partial interface IConstructorShape<TDeclaringType, TArgumentState> : IConstructorShape
{
    IEnumerable<IConstructorParameterShape> GetConstructorParameters();
}

public partial interface IConstructorParameterShape<TArgumentState, TParameterType> : IConstructorParameterShape
{
    ITypeShape<TParameterType> ParameterType { get; }
    Setter<TArgumentState, TParameterType> GetSetter();
}

public partial interface ITypeShapeVisitor
{
    object? VisitConstructor<TArgumentState, TParameterType>(IConstructorParameterShape<TArgumentState, TParameterType> shape, object? state = null);
}
```

Which exposes strongly typed setters for each of the constructor parameters. Putting it all together, here is toy implementation of a visitor that recursively constructs an object graph using constructor shapes:

```C#
class EmptyConstructorVisitor : TypeShapeVisitor
{
    private delegate void ParameterSetter<T>(ref T object);

    public override object? VisitObject<T>(ITypeShape<T> objectShape, object? _)
    {
        IConstructorShape? ctor = objectShape.GetConstructor();
        return ctor is null
            ? new Func<T>(() => default) // Just return the default if no ctor is found
            : ctor.Accept(this);
    }

    public override object? VisitConstructor<TDeclaringType, TArgumentState>(IConstructorShape<TDeclaringType, TArgumentState> constructorShape, object? _)
    {
        Func<TArgumentState> argumentStateCtor = constructorShape.GetArgumentStateConstructor();
        Func<TArgumentState, TDeclaringType> ctor = constructorShape.GetParameterizedConstructor();
        ParameterSetter<TArgumentState>[] parameterSetters = constructorShape.GetConstructorParameters()
            .Where(param => (ParameterSetter<TArgumentState>)param.Accept(this)!)
            .ToArray();

        return new Func<TDeclaringType>(() =>
        {
            TArgumentState state = argumentStateCtor();
            foreach (ParameterSetter<TArgumentState> parameterSetter in parameterSetters)
                parameterSetter(ref state);

            return ctor(state);
        });
    }

    public override object? VisitConstructorParameter<TArgumentState, TParameter>(IConstructorParameterShape<TArgumentState, TParameter> parameter, object? _)
    {
        var parameterFactory = (Func<TParameter>)parameter.ParameterType.Accept(this);
        Setter<TArgumentState, TParameter> setter = parameter.GetSetter();
        return new ParameterSetter<TArgumentState>(ref TArgumentState state => setter(ref state, parameterFactory()));
    }
}
```

We can now use the visitor to construct an empty instance factory:

```C#
ITypeShape<MyPoco> shape = provider.GetShape<MyPoco>();
EmptyConstructorVisitor visitor = new();
var factory = (Func<MyPoco>)shape.Accept(visitor)!;

MyPoco value = factory();
Console.WriteLine(value); // MyPoco { x = , y = 0 }

record MyPoco(int x, string y);
```

### Constructing collections

Collection types are constructed somewhat differently compared to regular POCOs, using one of the following strategies:

* The collection is mutable and can be populated following the conventions of [C# collection initializers](https://learn.microsoft.com/dotnet/csharp/programming-guide/classes-and-structs/object-and-collection-initializers).
* The collection can be constructed using a `ReadOnlySpan` of entries. Types declaring factories via the `CollectionBuilderAttribute` map to this strategy.
* The collection can be constructed using an `IEnumerable` of entries. Typically reserved for immutable collections that expose a relevant constructor.
* The collection type is not constructible.

These strategies are surfaced via the `CollectionConstructionStrategy` enum:

```C#
[Flags]
public enum CollectionConstructionStrategy
{
    None = 0,
    Mutable = 1,
    Span = 2,
    Enumerable = 4,
}
```

Which is exposed in the relevant shape types as follows:

```C#
public partial interface IEnumerableTypeShape<TEnumerable, TElement>
{
    CollectionConstructionStrategy ConstructionStrategy { get; }

    // Implemented by CollectionConstructionStrategy.Mutable types
    Func<TEnumerable> GetDefaultConstructor();
    Setter<TEnumerable, TElement> GetAddElement();

    // Implemented by CollectionConstructionStrategy.Span types
    SpanConstructor<TElement, TEnumerable> GetSpanConstructor();

    // Implemented by CollectionConstructionStrategy.Enumerable types
    Func<IEnumerable<TElement>, TEnumerable> GetEnumerableConstructor();
}

public delegate TEnumerable SpanConstructor<TElement, TEnumerable>(ReadOnlySpan<TElement> span);
```

Putting it all together, we can extend `EmptyConstructorVisitor` to collection types like so:

```C#
class EmptyConstructorVisitor : TypeShapeVisitor
{
    public override object? VisitEnumerable<TEnumerable, TElement>(IEnumerableTypeShape<TEnumerable, TElement> typeShape, object? _)
    {
        const int size = 10;
        var elementFactory = (Func<TElement>)typeShape.Accept(this);
        switch (typeShape.ConstructionStrategy)
        {
            case CollectionConstructionStrategy.Mutable:
                Func<TEnumerable> defaultCtor = typeShape.GetDefaultConstructor();
                Setter<TEnumerable, TElement> addElement = typeShape.GetAddElement();
                return new Func<TEnumerable>(() =>
                {
                    TEnumerable value = defaultCtor();
                    for (int i = 0; i < size; i++) addElement(ref value, elementFactory());
                    return value;
                });

            case CollectionConstructionStrategy.Span:
                SpanConstructor<TElement, TEnumerable> spanCtor = typeShape.GetSpanConstructor();
                return new Func<TEnumerable>(() =>
                {
                    var buffer = new TElement[size];
                    for (int i = 0; i < size; i++) buffer[i] = elementFactory();
                    return spanCtor(buffer);
                });

            case CollectionConstructionStrategy.Enumerable:
                Func<IEnumerable<TElement>, TEnumerable> enumerableCtor = typeShape.GetEnumerableConstructor();
                return new Func<TEnumerable>(() =>
                {
                    var buffer = new TElement[size];
                    for (int i = 0; i < size; i++) buffer[i] = elementFactory();
                    return enumerableCtor(buffer);
                });

            default:
                // No constructor, just return the default.
                return new Func<TEnumerable>(() => default!);
        }
    }
}
```

This concludes the tutorial for the core TypeShape programming model. For more detailed examples, please refer to the [`TypeShape.Examples`](https://github.com/eiriktsarpalis/typeshape-csharp/tree/main/src/TypeShape.Examples) project folder.
