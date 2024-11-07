using System.Reflection;
using PolyType.Abstractions;

namespace PolyType.SourceGenModel;

/// <summary>
/// Source generator model for a constructor parameter shape.
/// </summary>
/// <typeparam name="TArgumentState">The mutable constructor argument state type.</typeparam>
/// <typeparam name="TParameter">The constructor parameter type.</typeparam>
public sealed class SourceGenConstructorParameterShape<TArgumentState, TParameter> : IConstructorParameterShape<TArgumentState, TParameter>
{
    /// <summary>
    /// The position of the parameter in the constructor signature.
    /// </summary>
    public required int Position { get; init; }

    /// <summary>
    /// The name of the parameter.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The kind of the parameter.
    /// </summary>
    public required ConstructorParameterKind Kind { get; init; }

    /// <summary>
    /// Indicates whether the parameter is required.
    /// </summary>
    public required bool IsRequired { get; init; }

    /// <summary>
    /// Indicates whether the parameter is non-nullable.
    /// </summary>
    public required bool IsNonNullable { get; init; }

    /// <summary>
    /// Indicates whether the parameter is declared public.
    /// </summary>
    public required bool IsPublic { get; init; }

    /// <summary>
    /// The type shape of the parameter.
    /// </summary>
    public required ITypeShape<TParameter> ParameterType { get; init; }

    /// <summary>
    /// The setter for the parameter.
    /// </summary>
    public required Setter<TArgumentState, TParameter> Setter { get; init; }

    /// <summary>
    /// A constructor delegate for the custom attribute provider of the parameter.
    /// </summary>
    public Func<ICustomAttributeProvider?>? AttributeProviderFunc { get; init; }

    /// <summary>
    /// Indicates whether the parameter has a default value.
    /// </summary>
    public bool HasDefaultValue { get; init; }

    /// <summary>
    /// The default value of the parameter.
    /// </summary>
    public TParameter? DefaultValue { get; init; }

    object? IConstructorParameterShape.DefaultValue => HasDefaultValue ? DefaultValue : null;
    ITypeShape IConstructorParameterShape.ParameterType => ParameterType;
    ICustomAttributeProvider? IConstructorParameterShape.AttributeProvider => AttributeProviderFunc?.Invoke();
    Setter<TArgumentState, TParameter> IConstructorParameterShape<TArgumentState, TParameter>.GetSetter() => Setter;
}
