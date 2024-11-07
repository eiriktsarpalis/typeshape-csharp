using PolyType.Abstractions;

namespace PolyType.SourceGenModel;

/// <summary>
/// Source generator model for nullable types.
/// </summary>
/// <typeparam name="T">The element type of the nullable type.</typeparam>
public sealed class SourceGenNullableTypeShape<T> : INullableTypeShape<T>
    where T : struct
{
    /// <summary>
    /// The provider that generated this shape.
    /// </summary>
    public required ITypeShapeProvider Provider { get; init; }

    /// <summary>
    /// The shape of the element type.
    /// </summary>
    public required ITypeShape<T> ElementType { get; init; }
}
