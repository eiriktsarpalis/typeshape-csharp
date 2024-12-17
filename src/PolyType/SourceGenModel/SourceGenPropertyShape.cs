using System.Reflection;
using PolyType.Abstractions;

namespace PolyType.SourceGenModel;

/// <summary>
/// Source generator model for a property shape.
/// </summary>
/// <typeparam name="TDeclaringType">The declaring type of the property.</typeparam>
/// <typeparam name="TPropertyType">The type of the property value.</typeparam>
public sealed class SourceGenPropertyShape<TDeclaringType, TPropertyType> : IPropertyShape<TDeclaringType, TPropertyType>
{
    /// <summary>
    /// Gets the name of the property.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the custom attribute provider for the property.
    /// </summary>
    public Func<ICustomAttributeProvider?>? AttributeProviderFunc { get; init; }

    /// <summary>
    /// Gets the shape of the declaring type.
    /// </summary>
    public required IObjectTypeShape<TDeclaringType> DeclaringType { get; init; }

    /// <summary>
    /// Gets the shape of the property type.
    /// </summary>
    public required ITypeShape<TPropertyType> PropertyType { get; init; }

    /// <summary>
    /// Gets the getter delegate for the property.
    /// </summary>
    public Getter<TDeclaringType, TPropertyType>? Getter { get; init; }

    /// <summary>
    /// Gets the setter delegate for the property.
    /// </summary>
    public Setter<TDeclaringType, TPropertyType>? Setter { get; init; }

    /// <summary>
    /// Gets a value indicating whether the getter is declared public.
    /// </summary>
    public required bool IsGetterPublic { get; init; }

    /// <summary>
    /// Gets a value indicating whether the setter is declared public.
    /// </summary>
    public required bool IsSetterPublic { get; init; }

    /// <summary>
    /// Gets a value indicating whether the getter is non-nullable.
    /// </summary>
    public required bool IsGetterNonNullable { get; init; }

    /// <summary>
    /// Gets a value indicating whether the setter is non-nullable.
    /// </summary>
    public required bool IsSetterNonNullable { get; init; }

    /// <summary>
    /// Gets a value indicating whether the shape represents a field.
    /// </summary>
    public bool IsField { get; init; }

    Getter<TDeclaringType, TPropertyType> IPropertyShape<TDeclaringType, TPropertyType>.GetGetter()
        => Getter is { } getter ? getter : throw new InvalidOperationException("Property shape does not specify a getter.");

    Setter<TDeclaringType, TPropertyType> IPropertyShape<TDeclaringType, TPropertyType>.GetSetter()
        => Setter is { } setter ? setter : throw new InvalidOperationException("Property shape does not specify a setter.");

    ITypeShape IPropertyShape.PropertyType => PropertyType;
    IObjectTypeShape IPropertyShape.DeclaringType => DeclaringType;
    bool IPropertyShape.HasGetter => Getter is not null;
    bool IPropertyShape.HasSetter => Setter is not null;
    ICustomAttributeProvider? IPropertyShape.AttributeProvider => AttributeProviderFunc?.Invoke();
    object? IPropertyShape.Accept(ITypeShapeVisitor visitor, object? state) => visitor.VisitProperty(this, state);
}
