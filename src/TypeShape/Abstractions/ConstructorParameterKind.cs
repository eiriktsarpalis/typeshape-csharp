namespace TypeShape;

/// <summary>
/// Specifies the kind of constructor parameter.
/// </summary>
public enum ConstructorParameterKind
{
    /// <summary>
    /// Represents a constructor parameter.
    /// </summary>
    ConstructorParameter,

    /// <summary>
    /// Represents a property initializer.
    /// </summary>
    PropertyInitializer,

    /// <summary>
    /// Represents a field initializer.
    /// </summary>
    FieldInitializer,
}