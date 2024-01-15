namespace TypeShape.Roslyn;

/// <summary>
/// The kind of type represented by a <see cref="TypeDataModel"/>.
/// </summary>
public enum TypeDataKind
{
    /// <summary>
    /// Type is an <see cref="TypeDataModel"/>. Type is a primitive type or is a standalone value type (such as TimeSpan or DateTime).
    /// </summary>
    None = 0,

    /// <summary>
    /// Type is a <see cref="EnumDataModel"/>.
    /// </summary>
    Enum,

    /// <summary>
    /// Type is a <see cref="NullableDataModel"/>.
    /// </summary>
    Nullable,

    /// <summary>
    /// Type is a <see cref="EnumerableDataModel"/>.
    /// </summary>
    Enumerable,

    /// <summary>
    /// Type is a <see cref="DictionaryDataModel"/>.
    /// </summary>
    Dictionary,

    /// <summary>
    /// Type is a <see cref="ObjectDataModel"/>.
    /// </summary>
    Object,

    /// <summary>
    /// Type is a <see cref="TupleDataModel"/>.
    /// </summary>
    Tuple,
}
