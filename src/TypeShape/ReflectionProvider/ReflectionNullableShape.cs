namespace TypeShape.ReflectionProvider;

internal sealed class ReflectionNullableShape<T>(ReflectionTypeShapeProvider provider) : INullableShape<T>
    where T : struct
{
    public ITypeShape<T> ElementType => provider.GetShape<T>();
}
