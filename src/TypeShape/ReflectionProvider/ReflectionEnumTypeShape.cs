using TypeShape.Abstractions;

namespace TypeShape.ReflectionProvider;

internal sealed class ReflectionEnumTypeShape<TEnum, TUnderlying>(ReflectionTypeShapeProvider provider) : IEnumTypeShape<TEnum, TUnderlying>
    where TEnum : struct, Enum
{
    public ITypeShape<TUnderlying> UnderlyingType => provider.GetShape<TUnderlying>();
    public ITypeShapeProvider Provider => provider;
}
