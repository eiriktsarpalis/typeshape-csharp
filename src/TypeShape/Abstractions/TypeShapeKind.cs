namespace TypeShape.Abstractions;

/// <summary>
/// Defines kinds of an <see cref="ITypeShape"/> instance.
/// </summary>
public enum TypeShapeKind
{
    /// <summary>
    /// Shape represents a named type without added structure, but which can define property or constructor shapes.
    /// </summary>
    None = 0,

    /// <summary>
    /// Shape represents an enum type using <see cref="IEnumTypeShape"/>.
    /// </summary>
    Enum = 1,

    /// <summary>
    /// Shape represents a <see cref="Nullable{T}"/> using <see cref="INullableTypeShape"/>.
    /// </summary>
    Nullable = 2,

    /// <summary>
    /// Shape represents an enumerable type using <see cref="IEnumerableTypeShape"/>.
    /// </summary>
    Enumerable = 3,

    /// <summary>
    /// Shape represents a dictionary type using <see cref="IDictionaryTypeShape"/>.
    /// </summary>
    Dictionary = 4,
}
