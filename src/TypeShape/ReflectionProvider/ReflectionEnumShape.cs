namespace TypeShape.ReflectionProvider;

internal sealed class ReflectionEnumShape<TEnum, TUnderlying> : IEnumShape<TEnum, TUnderlying>
    where TEnum : struct, Enum
{
    private readonly ReflectionTypeShapeProvider _provider;

    public ReflectionEnumShape(ReflectionTypeShapeProvider provider)
        => _provider = provider;

    public ITypeShape Type => _provider.GetShape<TEnum>();
    public ITypeShape UnderlyingType => _provider.GetShape<TUnderlying>();

    public object? Accept(ITypeShapeVisitor visitor, object? state)
        => visitor.VisitEnum(this, state);
}
