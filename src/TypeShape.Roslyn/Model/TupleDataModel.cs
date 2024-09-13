using System.Collections.Immutable;

namespace TypeShape.Roslyn;

/// <summary>
/// Represents the data model for a tuple type.
/// </summary>
public sealed class TupleDataModel : TypeDataModel
{
    /// <inheritdoc/>
    public override TypeDataKind Kind => TypeDataKind.Tuple;

    /// <summary>
    /// The list of elements in the tuple. Tuples with more than 8 elements are flattened to a single list.
    /// </summary>
    public required ImmutableArray<PropertyDataModel> Elements { get; init; }

    /// <summary>
    /// Specifies whether the type is a <see cref="ValueTuple"/> or a <see cref="Tuple"/>.
    /// </summary>
    public required bool IsValueTuple { get; init; }
}
