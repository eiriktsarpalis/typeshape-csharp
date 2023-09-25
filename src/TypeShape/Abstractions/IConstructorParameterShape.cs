using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace TypeShape;

/// <summary>
/// Provides a strongly-typed shape model for a given .NET constructor parameter,
/// representing either an actual constructor parameter or a required or init-only property.
/// </summary>
public interface IConstructorParameterShape
{
    /// <summary>
    /// The 0-indexed position of the current constructor parameter.
    /// </summary>
    int Position { get; }

    /// <summary>
    /// The shape of the constructor parameter type.
    /// </summary>
    ITypeShape ParameterType { get; }

    /// <summary>
    /// The name of the constructor parameter.
    /// </summary>
    string? Name { get; }

    /// <summary>
    /// Indicates whether the parameter specifies a default value.
    /// </summary>
    bool HasDefaultValue { get; }

    /// <summary>
    /// The default value specified by the parameter, if applicable.
    /// </summary>
    object? DefaultValue { get; }

    /// <summary>
    /// Indicates whether a value is required by the current parameter.
    /// </summary>
    /// <remarks>
    /// A parameter is reported as required if it is either a 
    /// constructor parameter without a default value or a required property.
    /// </remarks>
    bool IsRequired { get; }

    /// <summary>
    /// Specifies whether the parameter requires non-null values.
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
    /// The provider used for parameter-level attribute resolution.
    /// </summary>
    ICustomAttributeProvider? AttributeProvider { get; }

    /// <summary>
    /// Accepts an <see cref="ITypeShapeVisitor"/> for strongly-typed traversal.
    /// </summary>
    /// <param name="visitor">The visitor to accept.</param>
    /// <param name="state">The state parameter to pass to the underlying visitor.</param>
    /// <returns>The <see cref="object?"/> result returned by the visitor.</returns>
    object? Accept(ITypeShapeVisitor visitor, object? state);
}

/// <summary>
/// Provides a strongly-typed shape model for a given .NET constructor parameter,
/// representing either an actual constructor parameter or a required or init-only property.
/// </summary>
/// <typeparam name="TArgumentState">The state type used for aggregating constructor arguments.</typeparam>
/// <typeparam name="TParameterType">The type of the underlying constructor parameter.</typeparam>
public interface IConstructorParameterShape<TArgumentState, TParameterType> : IConstructorParameterShape
{
    /// <summary>
    /// Creates a setter delegate for configuring a state object 
    /// with a value for the current argument.
    /// </summary>
    /// <returns>A <see cref="Setter{TDeclaringType, TPropertyType}"/> delegate.</returns>
    Setter<TArgumentState, TParameterType> GetSetter();

    /// <summary>
    /// The default value specified by the parameter, if applicable.
    /// </summary>
    new TParameterType? DefaultValue { get; }
}
