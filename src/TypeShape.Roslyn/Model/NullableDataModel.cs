using Microsoft.CodeAnalysis;

namespace TypeShape.Roslyn;

/// <summary>
/// Represents a <see cref="Nullable{T}"/> type.
/// </summary>
public sealed class NullableDataModel : TypeDataModel
{
    public override TypeDataKind Kind => TypeDataKind.Nullable;

    public required ITypeSymbol ElementType { get; init; }
}
