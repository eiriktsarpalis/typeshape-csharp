using System.Reflection;

namespace TypeShape;

/// <summary>
/// Provides a strongly-typed shape model for a given .NET instance property or field.
/// </summary>
public interface IPropertyShape
{
    /// <summary>
    /// The name of the property.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// The provider used for property-level attribute resolution.
    /// </summary>
    ICustomAttributeProvider? AttributeProvider { get; }

    /// <summary>
    /// The shape of the declaring type.
    /// </summary>
    ITypeShape DeclaringType { get; }

    /// <summary>
    /// The shape of the property type.
    /// </summary>
    ITypeShape PropertyType { get; }

    /// <summary>
    /// Specifies whether the property has an accessible getter.
    /// </summary>
    bool HasGetter { get; }

    /// <summary>
    /// Specifies whether the property has an accessible setter.
    /// </summary>
    bool HasSetter { get; }

    /// <summary>
    /// Specifies whether the shape represents a .NET field.
    /// </summary>
    bool IsField { get; }

    /// <summary>
    /// Specifies whether the getter returns a non-nullable reference type.
    /// </summary>
    bool IsGetterNonNullableReferenceType { get; }

    /// <summary>
    /// Specifies whether the setter accepts a non-nullable reference type.
    /// </summary>
    bool IsSetterNonNullableReferenceType { get; }

    /// <summary>
    /// Accepts an <see cref="ITypeShapeVisitor"/> for strongly typed traversal.
    /// </summary>
    /// <param name="visitor">The visitor to accept.</param>
    /// <param name="state">The state parameter to pass to the underlying visitor.</param>
    /// <returns>The <see cref="object?"/> result returned by the visitor.</returns>
    object? Accept(ITypeShapeVisitor visitor, object? state);
}

/// <summary>
/// Provides a strongly-typed shape model for a given .NET instance property or field.
/// </summary>
/// <typeparam name="TDeclaringType">The declaring type of the underlying property.</typeparam>
/// <typeparam name="TPropertyType">The property type of the underlying property.</typeparam>
public interface IPropertyShape<TDeclaringType, TPropertyType> : IPropertyShape
{
    /// <summary>
    /// Creates a getter delegate for the property, if applicable.
    /// </summary>
    /// <exception cref="InvalidOperationException">The property has no accessible getter.</exception>
    /// <returns>A getter delegate for the property.</returns>
    Getter<TDeclaringType, TPropertyType> GetGetter();

    /// <summary>
    /// Creates a setter delegate for the property, if applicable.
    /// </summary>
    /// <exception cref="InvalidOperationException">The property has no accessible setter.</exception>
    /// <returns>A setter delegate for the property.</returns>
    Setter<TDeclaringType, TPropertyType> GetSetter();
}