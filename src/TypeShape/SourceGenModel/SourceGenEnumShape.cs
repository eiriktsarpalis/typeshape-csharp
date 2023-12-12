namespace TypeShape.SourceGenModel;

public sealed class SourceGenEnumShape<TEnum, TUnderlying> : IEnumShape<TEnum, TUnderlying>
    where TEnum : struct, Enum
{
    public required ITypeShape<TEnum> Type { get; init; }
    public required ITypeShape<TUnderlying> UnderlyingType { get; init; }
}
