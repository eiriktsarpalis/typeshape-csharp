using System.Reflection;

namespace TypeShape.SourceGenModel;

/// <summary>
/// Source generator model for a property shape.
/// </summary>
/// <typeparam name="TDeclaringType">The declaring type of the property.</typeparam>
/// <typeparam name="TPropertyType">The type of the property value.</typeparam>
public sealed class SourceGenPropertyShape<TDeclaringType, TPropertyType> : IPropertyShape<TDeclaringType, TPropertyType>
{
    /// <summary>
    /// The name of the property.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The custom attribute provider for the property.
    /// </summary>
    public Func<ICustomAttributeProvider?>? AttributeProviderFunc { get; init; }

    /// <summary>
    /// The shape of the declaring type.
    /// </summary>
    public required ITypeShape<TDeclaringType> DeclaringType { get; init; }

    /// <summary>
    /// The shape of the property type.
    /// </summary>
    public required ITypeShape<TPropertyType> PropertyType { get; init; }

    /// <summary>
    /// The getter delegate for the property.
    /// </summary>
    public Getter<TDeclaringType, TPropertyType>? Getter { get; init; }

    /// <summary>
    /// The setter delegate for the property.
    /// </summary>
    public Setter<TDeclaringType, TPropertyType>? Setter { get; init; }

    /// <summary>
    /// Whether the getter is declared public.
    /// </summary>
    public required bool IsGetterPublic { get; init; }

    /// <summary>
    /// Whether the setter is declared public.
    /// </summary>
    public required bool IsSetterPublic { get; init; }

    /// <summary>
    /// Whether the getter is non-nullable.
    /// </summary>
    public required bool IsGetterNonNullable { get; init; }

    /// <summary>
    /// Whether the setter is non-nullable.
    /// </summary>
    public required bool IsSetterNonNullable { get; init; }

    /// <summary>
    /// Whether the shape represents a field.
    /// </summary>
    public bool IsField { get; init; }

    Getter<TDeclaringType, TPropertyType> IPropertyShape<TDeclaringType, TPropertyType>.GetGetter()
        => Getter is { } getter ? getter : throw new InvalidOperationException("Property shape does not specify a getter.");

    Setter<TDeclaringType, TPropertyType> IPropertyShape<TDeclaringType, TPropertyType>.GetSetter()
        => Setter is { } setter ? setter : throw new InvalidOperationException("Property shape does not specify a setter.");

    bool IPropertyShape.HasGetter => Getter is not null;
    bool IPropertyShape.HasSetter => Setter is not null;
    ICustomAttributeProvider? IPropertyShape.AttributeProvider => AttributeProviderFunc?.Invoke();
}
