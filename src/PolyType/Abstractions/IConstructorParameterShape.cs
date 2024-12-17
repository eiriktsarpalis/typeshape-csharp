using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace PolyType.Abstractions;

/// <summary>
/// Provides a strongly typed shape model for a given .NET constructor parameter,
/// representing either an actual constructor parameter or a member initializer.
/// </summary>
public interface IConstructorParameterShape
{
    /// <summary>
    /// Gets the 0-indexed position of the current constructor parameter.
    /// </summary>
    int Position { get; }

    /// <summary>
    /// Gets the shape of the constructor parameter type.
    /// </summary>
    ITypeShape ParameterType { get; }

    /// <summary>
    /// Gets the name of the constructor parameter.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets specifies the kind of the current parameter.
    /// </summary>
    ConstructorParameterKind Kind { get; }

    /// <summary>
    /// Gets a value indicating whether the parameter has a default value.
    /// </summary>
    bool HasDefaultValue { get; }

    /// <summary>
    /// Gets the default value specified by the parameter, if applicable.
    /// </summary>
    object? DefaultValue { get; }

    /// <summary>
    /// Gets a value indicating whether a value is required for the current parameter.
    /// </summary>
    /// <remarks>
    /// A parameter is reported as required if it is either a
    /// constructor parameter without a default value or a required property.
    /// </remarks>
    bool IsRequired { get; }

    /// <summary>
    /// Gets a value indicating whether the parameter requires non-null values.
    /// </summary>
    /// <remarks>
    /// Returns <see langword="true" /> if the parameter type is a non-nullable struct, a non-nullable reference type
    /// or the parameter has been annotated with the <see cref="DisallowNullAttribute"/>.
    ///
    /// Conversely, it could return <see langword="false"/> if a non-nullable parameter
    /// has been annotated with <see cref="AllowNullAttribute"/>.
    /// </remarks>
    bool IsNonNullable { get; }

    /// <summary>
    /// Gets a value indicating whether the parameter is a public property or field initializer.
    /// </summary>
    bool IsPublic { get; }

    /// <summary>
    /// Gets the provider used for parameter-level attribute resolution.
    /// </summary>
    ICustomAttributeProvider? AttributeProvider { get; }

    /// <summary>
    /// Accepts an <see cref="ITypeShapeVisitor"/> for strongly-typed traversal.
    /// </summary>
    /// <param name="visitor">The visitor to accept.</param>
    /// <param name="state">The state parameter to pass to the underlying visitor.</param>
    /// <returns>The <see cref="object"/> result returned by the visitor.</returns>
    object? Accept(ITypeShapeVisitor visitor, object? state = null);
}

/// <summary>
/// Provides a strongly typed shape model for a given .NET constructor parameter,
/// representing either an actual constructor parameter or a required or init-only property.
/// </summary>
/// <typeparam name="TArgumentState">The state type used for aggregating constructor arguments.</typeparam>
/// <typeparam name="TParameterType">The type of the underlying constructor parameter.</typeparam>
public interface IConstructorParameterShape<TArgumentState, TParameterType> : IConstructorParameterShape
{
    /// <summary>
    /// Gets the shape of the constructor parameter type.
    /// </summary>
    new ITypeShape<TParameterType> ParameterType { get; }

    /// <summary>
    /// Gets the default value specified by the parameter, if applicable.
    /// </summary>
    new TParameterType? DefaultValue { get; }

    /// <summary>
    /// Creates a setter delegate for configuring a state object
    /// with a value for the current argument.
    /// </summary>
    /// <returns>A <see cref="Setter{TDeclaringType, TPropertyType}"/> delegate.</returns>
    Setter<TArgumentState, TParameterType> GetSetter();
}