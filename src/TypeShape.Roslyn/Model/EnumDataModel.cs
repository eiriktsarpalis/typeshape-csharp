using Microsoft.CodeAnalysis;

namespace TypeShape.Roslyn;

/// <summary>
/// Represents an <see cref="Enum"/> type data model.
/// </summary>
public sealed class EnumDataModel : TypeDataModel
{
    public override TypeDataKind Kind => TypeDataKind.Enum;

    /// <summary>
    /// The underlying numeric type used by the enum.
    /// </summary>
    public required ITypeSymbol UnderlyingType { get; init; }
}
