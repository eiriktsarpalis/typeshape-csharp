using System.Collections;
using System.Runtime.CompilerServices;
using PolyType.Abstractions;

namespace PolyType.Tests;

/// <summary>
/// Represents a test case whose shape is provided by an <see cref="IShapeable{T}"/> implementation.
/// </summary>
/// <typeparam name="T">The type of the value being tested.</typeparam>
/// <typeparam name="TProvider">The type of the shape provider.</typeparam>
/// <param name="Value">The value being tested.</param>
public sealed record TestCase<T, TProvider>(T? Value) : TestCase<T>(Value, TProvider.GetShape())
    where TProvider : IShapeable<T>;

/// <summary>
/// Represents a test case instance.
/// </summary>
/// <typeparam name="T">The type of the value being tested.</typeparam>
/// <param name="Value">The value being tested.</param>
/// <param name="DefaultShape">The default type shape of the value, typically source generated.</param>
public record TestCase<T>(T? Value, ITypeShape<T> DefaultShape) : ITestCase
{
    /// <summary>
    /// Additional test values to be used by the <see cref="ITestCase.ExpandCases"/> implementation.
    /// </summary>
    public T?[]? AdditionalValues { get; init; }

    /// <summary>
    /// Gets a value indicating whether the type is a LIFO collection that serializes elements in reverse order.
    /// </summary>
    public bool IsStack { get; init; }

    /// <summary>
    /// Gets a value indicating whether the type has constructor parameters that are passed by reference.
    /// </summary>
    public bool HasRefConstructorParameters { get; init; }

    /// <summary>
    /// Gets a value indicating whether the type has out constructor parameters.
    /// </summary>
    public bool HasOutConstructorParameters { get; init; }

    /// <summary>
    /// Gets a value indicating whether the type uses a constructor that accepts a span.
    /// </summary>
    public bool UsesSpanConstructor { get; init; }

    /// <summary>
    /// Gets a value indicating whether the type is a simple value that can be checked for equality.
    /// </summary>
    public bool IsEquatable =>
        typeof(IEquatable<T>).IsAssignableFrom(typeof(T)) &&
        !typeof(T).IsImmutableArray() &&
        !typeof(T).IsMemoryType(out _, out _) &&
        !typeof(T).IsRecordType();

    /// <summary>
    /// Gets a value indicating whether the type is a tuple.
    /// </summary>
    public bool IsTuple => typeof(ITuple).IsAssignableFrom(typeof(T));

    /// <summary>
    /// Gets a value indicating whether the type is a tuple containing > 8 elements.
    /// </summary>
    public bool IsLongTuple => IsTuple && typeof(T).GetMember("Rest").Length > 0;

    /// <summary>
    /// Gets a value indicating whether the type is a multi-dimensional array.
    /// </summary>
    public bool IsMultiDimensionalArray => typeof(T).IsArray && typeof(T).GetArrayRank() != 1;

    /// <summary>
    /// Gets a value indicating whether the type is an abstract class or an interface.
    /// </summary>
    public bool IsAbstract => typeof(T).IsAbstract || typeof(T).IsInterface;

    Type ITestCase.Type => typeof(T);
    object? ITestCase.Value => Value;
    ITypeShape ITestCase.DefaultShape => DefaultShape;
    IEnumerable<ITestCase> ITestCase.ExpandCases()
    {
        yield return this with { AdditionalValues = [] };

        if (default(T) is null && Value is not null)
        {
            yield return this with { Value = default, AdditionalValues = [] };
        }

        foreach (T? additionalValue in AdditionalValues ?? [])
        {
            yield return this with { Value = additionalValue, AdditionalValues = [] };
        }
    }
}