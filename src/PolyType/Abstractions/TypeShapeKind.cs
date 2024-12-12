namespace PolyType.Abstractions;

/// <summary>
/// Defines kinds of an <see cref="ITypeShape"/> instance.
/// </summary>
#if IS_MAIN_POLYTYPE_PROJECT
public
#else
internal
#endif
enum TypeShapeKind
{
    /// <summary>
    /// Default value not representing any specific shape.
    /// </summary>
    None = 0,

    /// <summary>
    /// Shape represents an object type using <see cref="IObjectTypeShape"/>.
    /// </summary>
    Object = 1,

    /// <summary>
    /// Shape represents an enum type using <see cref="IEnumTypeShape"/>.
    /// </summary>
    Enum = 2,

    /// <summary>
    /// Shape represents a <see cref="Nullable{T}"/> using <see cref="INullableTypeShape"/>.
    /// </summary>
    Nullable = 3,

    /// <summary>
    /// Shape represents an enumerable type using <see cref="IEnumerableTypeShape"/>.
    /// </summary>
    Enumerable = 4,

    /// <summary>
    /// Shape represents a dictionary type using <see cref="IDictionaryTypeShape"/>.
    /// </summary>
    Dictionary = 5,
}
