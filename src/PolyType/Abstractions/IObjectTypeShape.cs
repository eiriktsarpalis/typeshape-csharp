namespace PolyType.Abstractions;

/// <summary>
/// Provides a strongly typed shape model for a .NET object.
/// </summary>
public interface IObjectTypeShape : ITypeShape
{
    /// <summary>
    /// Determines whether the current shape represents a C# record type.
    /// </summary>
    bool IsRecordType { get; }

    /// <summary>
    /// Determines whether the current shape represents a tuple type, either <see cref="System.Tuple"/> or <see cref="System.ValueTuple"/>.
    /// </summary>
    bool IsTupleType { get; }

    /// <summary>
    /// Determines whether the current type defines any property shapes.
    /// </summary>
    bool HasProperties { get; }

    /// <summary>
    /// Determines whether the current type includes a constructor shape.
    /// </summary>
    bool HasConstructor { get; }

    /// <summary>
    /// Gets all available property/field shapes for the given type.
    /// </summary>
    /// <returns>An enumeration of all available property/field shapes.</returns>
    IEnumerable<IPropertyShape> GetProperties();

    /// <summary>
    /// Gets the constructor shape for the given type, if available.
    /// </summary>
    /// <returns>A <see cref="IConstructorShape"/> representation of the constructor.</returns>
    IConstructorShape? GetConstructor();
}

/// <summary>
/// Provides a strongly typed shape model for a .NET object.
/// </summary>
/// <typeparam name="T">The type of .NET object.</typeparam>
public interface IObjectTypeShape<T> : ITypeShape<T>, IObjectTypeShape;