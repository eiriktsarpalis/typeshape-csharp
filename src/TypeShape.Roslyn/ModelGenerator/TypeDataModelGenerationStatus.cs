namespace TypeShape.Roslyn;

/// <summary>
/// Model generation result returned for a requested <see cref="ITypeSymbol"/>.
/// </summary>
public enum TypeDataModelGenerationStatus
{
    /// <summary>
    /// A data model was successfully generated for a given type.
    /// </summary>
    Success = 0,

    /// <summary>
    /// The type, or a type it depends on, is not supported.
    /// </summary>
    UnsupportedType = 1,

    /// <summary>
    /// The type, or a type it depends on, is not accessible to the source generator.
    /// </summary>
    InaccessibleType = 2,
};
