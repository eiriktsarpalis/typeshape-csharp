namespace TypeShape.ReflectionProvider;

internal class ReflectionNullableType<T> : INullableType<T>
    where T : struct
{
    private readonly ReflectionTypeShapeProvider _provider;
    public ReflectionNullableType(ReflectionTypeShapeProvider provider)
        => _provider = provider;

    public IType NullableType => _provider.GetShape<T?>();
    public IType ElementType => _provider.GetShape<T>();

    public object? Accept(INullableTypeVisitor visitor, object? state)
        => visitor.VisitNullable(this, state);
}
