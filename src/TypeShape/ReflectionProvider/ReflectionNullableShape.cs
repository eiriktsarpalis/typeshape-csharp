namespace TypeShape.ReflectionProvider;

internal sealed class ReflectionNullableShape<T>(ReflectionTypeShapeProvider provider) : INullableShape<T>
    where T : struct
{
    public ITypeShape Type => provider.GetShape<T?>();
    public ITypeShape ElementType => provider.GetShape<T>();

    public object? Accept(ITypeShapeVisitor visitor, object? state)
        => visitor.VisitNullable(this, state);
}
