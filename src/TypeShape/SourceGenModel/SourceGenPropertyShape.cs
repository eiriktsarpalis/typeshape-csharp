using System.Reflection;

namespace TypeShape.SourceGenModel;

public sealed class SourceGenPropertyShape<TDeclaringType, TPropertyType>(bool nonPublic) : IPropertyShape<TDeclaringType, TPropertyType>
{
    public required string Name { get; init; }
    public Func<ICustomAttributeProvider?>? AttributeProviderFunc { get; init; }
    public ICustomAttributeProvider? AttributeProvider => AttributeProviderFunc?.Invoke();

    public required ITypeShape DeclaringType { get; init; }
    public required ITypeShape PropertyType { get; init; }

    public Getter<TDeclaringType, TPropertyType>? Getter { get; init; }
    public Setter<TDeclaringType, TPropertyType>? Setter { get; init; }

    public bool HasGetter => Getter is not null && (IsGetterPublic || nonPublic);
    public bool HasSetter => Setter is not null && (IsSetterPublic || nonPublic);
    public required bool IsGetterPublic { get; init; }
    public required bool IsSetterPublic { get; init; }
    public required bool IsGetterNonNullable { get; init; }
    public required bool IsSetterNonNullable { get; init; }
    public bool IsField { get; init; }

    public object? Accept(ITypeShapeVisitor visitor, object? state)
        => visitor.VisitProperty(this, state);

    public Getter<TDeclaringType, TPropertyType> GetGetter()
        => HasGetter ? Getter! : throw new InvalidOperationException("Property shape does not specify a getter.");

    public Setter<TDeclaringType, TPropertyType> GetSetter()
        => HasSetter ? Setter! : throw new InvalidOperationException("Property shape does not specify a setter.");

    bool IPropertyShape.IsGetterNonNullable => IsGetterNonNullable && HasGetter;
    bool IPropertyShape.IsSetterNonNullable => IsSetterNonNullable && HasSetter;
}
