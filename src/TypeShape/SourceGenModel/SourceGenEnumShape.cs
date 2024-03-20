namespace TypeShape.SourceGenModel;

/// <summary>
/// Source generator model for enum shapes.
/// </summary>
/// <typeparam name="TEnum">The type of the enum.</typeparam>
/// <typeparam name="TUnderlying">The type of the underlying type of the enum.</typeparam>
public sealed class SourceGenEnumShape<TEnum, TUnderlying> : IEnumShape<TEnum, TUnderlying>
    where TEnum : struct, Enum
{
    /// <summary>
    /// The shape of the enum.
    /// </summary>
    public required ITypeShape<TEnum> Type { get; init; }

    /// <summary>
    /// The shape of the underlying type of the enum.
    /// </summary>
    public required ITypeShape<TUnderlying> UnderlyingType { get; init; }
}
