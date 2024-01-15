using Microsoft.CodeAnalysis;
using TypeShape.Roslyn.Helpers;

namespace TypeShape.Roslyn;

/// <summary>
/// A constructor parameter data model wrapping an <see cref="IParameterSymbol"/>.
/// </summary>
public readonly struct ConstructorParameterDataModel
{
    /// <summary>
    /// The parameter symbol that this model represents.
    /// </summary>
    public required IParameterSymbol Parameter { get; init; }

    /// <summary>
    /// True if parameter is a reference type declared as non-nullable.
    /// </summary>
    public bool IsNonNullable => Parameter.IsNonNullableAnnotation();

    /// <summary>
    /// Whether the parameter declares a default value.
    /// </summary>
    public bool HasDefaultValue => Parameter.HasExplicitDefaultValue;

    /// <summary>
    /// The default value literal expressed as a valid C# expression.
    /// </summary>
    public string? DefaultValueExpr => Parameter.FormatDefaultValueExpr();
}