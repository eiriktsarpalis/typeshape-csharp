using PolyType.Abstractions;
using System.Reflection;

namespace PolyType.ReflectionProvider;

internal abstract class ReflectionTypeShape<T>(ReflectionTypeShapeProvider provider) : ITypeShape<T>
{
    public abstract TypeShapeKind Kind { get; }
    public abstract object? Accept(ITypeShapeVisitor visitor, object? state = null);
    public ReflectionTypeShapeProvider Provider => provider;
    public Type Type => typeof(T);

    ITypeShapeProvider ITypeShape.Provider => provider;
    ICustomAttributeProvider? ITypeShape.AttributeProvider => typeof(T);
    object? ITypeShape.Invoke(ITypeShapeFunc function, object? state) => function.Invoke(this, state);
}
