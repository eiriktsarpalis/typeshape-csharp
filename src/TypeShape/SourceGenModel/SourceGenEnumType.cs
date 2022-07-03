namespace TypeShape.SourceGenModel;

public class SourceGenEnumType<TEnum, TUnderlying> : IEnumType<TEnum, TUnderlying>
    where TEnum : struct, Enum
{
    public required IType EnumType { get; init; }
    public required IType UnderlyingType { get; init; }

    public object? Accept(IEnumTypeVisitor visitor, object? state)
        => visitor.VisitEnum(this, state);
}
