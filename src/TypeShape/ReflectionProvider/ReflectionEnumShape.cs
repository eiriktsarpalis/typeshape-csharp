namespace TypeShape.ReflectionProvider;

internal sealed class ReflectionEnumShape<TEnum, TUnderlying>(ReflectionTypeShapeProvider provider) : IEnumShape<TEnum, TUnderlying>
    where TEnum : struct, Enum
{
    public ITypeShape Type => provider.GetShape<TEnum>();
    public ITypeShape UnderlyingType => provider.GetShape<TUnderlying>();

    public object? Accept(ITypeShapeVisitor visitor, object? state)
        => visitor.VisitEnum(this, state);
}
