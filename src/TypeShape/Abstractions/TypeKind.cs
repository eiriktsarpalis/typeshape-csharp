namespace TypeShape;

/// <summary>
/// Defines the special shapes provided by a <see cref="ITypeShape"/> instance.
/// </summary>
[Flags]
public enum TypeKind
{
    /// <summary>
    /// Type does not provide any special kinds, it is either a simple value or a POCO with properties.
    /// </summary>
    None = 0,
    /// <summary>
    /// Type provides a <see cref="IEnumShape"/> shape implementation.
    /// </summary>
    Enum = 1,
    /// <summary>
    /// Type provides a <see cref="INullableShape"/> shape implementation.
    /// </summary>
    Nullable = 2,
    /// <summary>
    /// Type provides a <see cref="IEnumerableShape"/> shape implementation.
    /// </summary>
    Enumerable = 4,
    /// <summary>
    /// Type provides a <see cref="IDictionaryShape"/> shape implementation.
    /// </summary>
    Dictionary = 8,
    /// <summary>
    /// Type provides <see cref="IPropertyShape"/> or <see cref="IConstructorShape"/> implementations.
    /// </summary>
    Object = 16,
}
