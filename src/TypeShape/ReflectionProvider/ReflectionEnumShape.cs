namespace TypeShape.ReflectionProvider;

internal sealed class ReflectionEnumShape<TEnum, TUnderlying>(ReflectionTypeShapeProvider provider) : IEnumShape<TEnum, TUnderlying>
    where TEnum : struct, Enum
{
    public ITypeShape<TEnum> Type => provider.GetShape<TEnum>();
    public ITypeShape<TUnderlying> UnderlyingType => provider.GetShape<TUnderlying>();
}
