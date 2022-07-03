using System.Reflection;

namespace TypeShape.SourceGenModel;

public class SourceGenProperty<TDeclaringType, TPropertyType> : IProperty<TDeclaringType, TPropertyType>
{
    public required string Name { get; init; }
    public ICustomAttributeProvider? AttributeProvider { get; init; }

    public required IType<TDeclaringType> DeclaringType { get; init; }
    public required IType<TPropertyType> PropertyType { get; init; }

    public Getter<TDeclaringType, TPropertyType>? Getter { get; init; }
    public Setter<TDeclaringType, TPropertyType>? Setter { get; init; }

    public bool HasGetter => Getter is not null;
    public bool HasSetter => Setter is not null;

    public bool IsRequired { get; init; }
    public bool IsInitOnly { get; init; }
    public bool IsField { get; init; }

    IType IProperty.DeclaringType => DeclaringType;
    IType IProperty.PropertyType => PropertyType;

    public object? Accept(IPropertyVisitor visitor, object? state)
        => visitor.VisitProperty(this, state);

    public Getter<TDeclaringType, TPropertyType> GetGetter()
    {
        if (Getter is null)
            throw new InvalidOperationException();

        return Getter;
    }

    public Setter<TDeclaringType, TPropertyType> GetSetter()
    {
        if (Setter is null)
            throw new InvalidOperationException();

        return Setter;
    }
}
