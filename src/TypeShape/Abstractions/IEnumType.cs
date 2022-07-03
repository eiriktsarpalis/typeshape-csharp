namespace TypeShape;

public interface IEnumType
{
    IType EnumType { get; }
    IType UnderlyingType { get; }
    object? Accept(IEnumTypeVisitor visitor, object? state);
}

public interface IEnumType<TEnum, TUnderlying> : IEnumType
    where TEnum : struct, Enum
{
}

public interface IEnumTypeVisitor
{
    object? VisitEnum<TEnum, TUnderlying>(IEnumType<TEnum, TUnderlying> enumType, object? state) where TEnum : struct, Enum;
}