using PolyType.Abstractions;

namespace PolyType.ReflectionProvider;

internal sealed class ReflectionNullableTypeShape<T>(ReflectionTypeShapeProvider provider) : ReflectionTypeShape<T?>(provider), INullableTypeShape<T>
    where T : struct
{
    public override TypeShapeKind Kind => TypeShapeKind.Nullable;
    public override object? Accept(ITypeShapeVisitor visitor, object? state = null) => visitor.VisitNullable(this, state);
    public ITypeShape<T> ElementType => Provider.GetShape<T>();
    ITypeShape INullableTypeShape.ElementType => ElementType;
}
