namespace TypeShape.ReflectionProvider;

internal sealed class ReflectionNullableShape<T> : INullableShape<T>
    where T : struct
{
    private readonly ReflectionTypeShapeProvider _provider;
    public ReflectionNullableShape(ReflectionTypeShapeProvider provider)
        => _provider = provider;

    public ITypeShape Type => _provider.GetShape<T?>();
    public ITypeShape ElementType => _provider.GetShape<T>();

    public object? Accept(ITypeShapeVisitor visitor, object? state)
        => visitor.VisitNullable(this, state);
}
