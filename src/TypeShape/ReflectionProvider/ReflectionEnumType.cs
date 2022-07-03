namespace TypeShape.ReflectionProvider;

internal class ReflectionEnumType<TEnum, TUnderlying> : IEnumType<TEnum, TUnderlying>
    where TEnum : struct, Enum
{
    private readonly ReflectionTypeShapeProvider _provider;

    public ReflectionEnumType(ReflectionTypeShapeProvider provider)
        => _provider = provider;

    public IType EnumType => _provider.GetShape<TEnum>();
    public IType UnderlyingType => _provider.GetShape<TUnderlying>();

    public object? Accept(IEnumTypeVisitor visitor, object? state)
        => visitor.VisitEnum(this, state);
}
