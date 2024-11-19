using PolyType.Abstractions;

namespace PolyType.SourceGenModel;

/// <summary>
/// Source generator model for enum shapes.
/// </summary>
/// <typeparam name="TEnum">The type of the enum.</typeparam>
/// <typeparam name="TUnderlying">The type of the underlying type of the enum.</typeparam>
public sealed class SourceGenEnumTypeShape<TEnum, TUnderlying> : SourceGenTypeShape<TEnum>, IEnumTypeShape<TEnum, TUnderlying>
    where TEnum : struct, Enum
{
    /// <summary>
    /// The shape of the underlying type of the enum.
    /// </summary>
    public required ITypeShape<TUnderlying> UnderlyingType { get; init; }

    /// <inheritdoc/>
    public override TypeShapeKind Kind => TypeShapeKind.Enum;

    /// <inheritdoc/>
    public override object? Accept(ITypeShapeVisitor visitor, object? state = null) => visitor.VisitEnum(this, state);

    ITypeShape IEnumTypeShape.UnderlyingType => UnderlyingType;
}
