using PolyType.Abstractions;

namespace PolyType.SourceGenModel;

/// <summary>
/// Source generator model for nullable types.
/// </summary>
/// <typeparam name="T">The element type of the nullable type.</typeparam>
public sealed class SourceGenNullableTypeShape<T> : SourceGenTypeShape<T?>, INullableTypeShape<T>
    where T : struct
{
    /// <summary>
    /// Gets the shape of the element type.
    /// </summary>
    public required ITypeShape<T> ElementType { get; init; }

    /// <inheritdoc/>
    public override TypeShapeKind Kind => TypeShapeKind.Nullable;

    /// <inheritdoc/>
    public override object? Accept(ITypeShapeVisitor visitor, object? state = null) => visitor.VisitNullable(this, state);

    ITypeShape INullableTypeShape.ElementType => ElementType;
}
