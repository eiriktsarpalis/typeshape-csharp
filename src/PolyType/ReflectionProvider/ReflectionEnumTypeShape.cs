using PolyType.Abstractions;

namespace PolyType.ReflectionProvider;

internal sealed class ReflectionEnumTypeShape<TEnum, TUnderlying>(ReflectionTypeShapeProvider provider) : ReflectionTypeShape<TEnum>(provider), IEnumTypeShape<TEnum, TUnderlying>
    where TEnum : struct, Enum
{
    public override TypeShapeKind Kind => TypeShapeKind.Enum;
    public override object? Accept(ITypeShapeVisitor visitor, object? state = null) => visitor.VisitEnum(this, state);
    public ITypeShape<TUnderlying> UnderlyingType => Provider.GetShape<TUnderlying>();
    ITypeShape IEnumTypeShape.UnderlyingType => UnderlyingType;
}
