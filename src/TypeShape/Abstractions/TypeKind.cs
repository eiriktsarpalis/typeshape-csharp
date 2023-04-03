namespace TypeShape;

/// <summary>
/// Defines the special shapes provided by a <see cref="IType"/> instance.
/// </summary>
[Flags]
public enum TypeKind
{
    /// <summary>
    /// Type does not provide any special kinds, it is either a simple value or a POCO with properties.
    /// </summary>
    None = 0,
    /// <summary>
    /// Type provides a <see cref="IEnumType"/> shape implementation.
    /// </summary>
    Enum = 1,
    /// <summary>
    /// Type provides a <see cref="INullableType"/> shape implementation.
    /// </summary>
    Nullable = 2,
    /// <summary>
    /// Type provides a <see cref="IEnumerableType"/> shape implementation.
    /// </summary>
    Enumerable = 4,
    /// <summary>
    /// Type provides a <see cref="IDictionaryType"/> shape implementation.
    /// </summary>
    Dictionary = 8,
}
