using Microsoft.CodeAnalysis;

namespace PolyType.Roslyn;

/// <summary>
/// Represents a <see cref="Nullable{T}"/> type.
/// </summary>
public sealed class NullableDataModel : TypeDataModel
{
    /// <inheritdoc/>
    public override TypeDataKind Kind => TypeDataKind.Nullable;

    /// <summary>
    /// Gets the element type of the nullable type.
    /// </summary>
    public required ITypeSymbol ElementType { get; init; }
}
