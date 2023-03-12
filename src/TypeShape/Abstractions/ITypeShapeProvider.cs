namespace TypeShape;

public interface ITypeShapeProvider
{
    IType<T>? GetShape<T>();
    IType? GetShape(Type type);
}
