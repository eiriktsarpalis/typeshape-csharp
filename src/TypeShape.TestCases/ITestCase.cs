using TypeShape.Abstractions;

namespace TypeShape.Tests;

/// <summary>
/// Represents a test case for a type shape and its value.
/// </summary>
public interface ITestCase
{
    /// <summary>
    /// Gets the underlying type that the test case represents.
    /// </summary>
    Type Type { get; }

    /// <summary>
    /// Gets the value being tested by the test case.
    /// </summary>
    object? Value { get; }

    /// <summary>
    /// Gets the default shape for the test case, typically produced by a source generator.
    /// </summary>
    ITypeShape DefaultShape { get; }

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
    public bool IsEquatable { get; }

    /// <summary>
    /// Gets a value indicating whether the type is a tuple.
    /// </summary>
    public bool IsTuple { get; }

    /// <summary>
    /// Gets a value indicating whether the type is a tuple containing > 8 elements.
    /// </summary>
    public bool IsLongTuple { get; }

    /// <summary>
    /// Gets a value indicating whether the type is a multi-dimensional array.
    /// </summary>
    public bool IsMultiDimensionalArray { get; }

    /// <summary>
    /// Gets a value indicating whether the type is an abstract class or an interface.
    /// </summary>
    public bool IsAbstract { get; }

    /// <summary>
    /// Expands the test case into multiple test cases.
    /// </summary>
    IEnumerable<ITestCase> ExpandCases();
}
