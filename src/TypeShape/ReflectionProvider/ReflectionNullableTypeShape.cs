namespace TypeShape.ReflectionProvider;

internal sealed class ReflectionNullableTypeShape<T>(ReflectionTypeShapeProvider provider) : INullableTypeShape<T>
    where T : struct
{
    public ITypeShape<T> ElementType => provider.GetShape<T>();
    public ITypeShapeProvider Provider => provider;
}
