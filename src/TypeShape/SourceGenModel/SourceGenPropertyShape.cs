using System.Reflection;

namespace TypeShape.SourceGenModel;

public sealed class SourceGenPropertyShape<TDeclaringType, TPropertyType> : IPropertyShape<TDeclaringType, TPropertyType>
{
    public required string Name { get; init; }
    public Func<ICustomAttributeProvider?>? AttributeProviderFunc { get; init; }
    public ICustomAttributeProvider? AttributeProvider => AttributeProviderFunc?.Invoke();

    public required ITypeShape DeclaringType { get; init; }
    public required ITypeShape PropertyType { get; init; }

    public Getter<TDeclaringType, TPropertyType>? Getter { get; init; }
    public Setter<TDeclaringType, TPropertyType>? Setter { get; init; }

    public bool HasGetter => Getter is not null;
    public bool HasSetter => Setter is not null;
    public bool IsField { get; init; }

    public object? Accept(ITypeShapeVisitor visitor, object? state)
        => visitor.VisitProperty(this, state);

    public Getter<TDeclaringType, TPropertyType> GetGetter()
    {
        if (Getter is null)
            throw new InvalidOperationException("Property shape does not specify a getter.");

        return Getter;
    }

    public Setter<TDeclaringType, TPropertyType> GetSetter()
    {
        if (Setter is null)
            throw new InvalidOperationException("Property shape does not specify a setter.");

        return Setter;
    }
}
