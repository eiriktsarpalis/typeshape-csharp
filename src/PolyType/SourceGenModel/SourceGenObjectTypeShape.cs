using PolyType.Abstractions;

namespace PolyType.SourceGenModel;

/// <summary>
/// Source generator model for object type shapes.
/// </summary>
/// <typeparam name="TObject">The type whose shape is described.</typeparam>
public sealed class SourceGenObjectTypeShape<TObject> : SourceGenTypeShape<TObject>, IObjectTypeShape<TObject>
{
    /// <summary>
    /// Whether the type represents a record.
    /// </summary>
    public required bool IsRecordType { get; init; }

    /// <summary>
    /// Whether the type represents a tuple.
    /// </summary>
    public required bool IsTupleType { get; init; }

    /// <summary>
    /// The factory method for creating property shapes.
    /// </summary>
    public Func<IEnumerable<IPropertyShape>>? CreatePropertiesFunc { get; init; }

    /// <summary>
    /// The factory method for creating constructor shapes.
    /// </summary>
    public Func<IConstructorShape>? CreateConstructorFunc { get; init; }

    /// <inheritdoc/>
    public override TypeShapeKind Kind => TypeShapeKind.Object;

    /// <inheritdoc/>
    public override object? Accept(ITypeShapeVisitor visitor, object? state = null) => visitor.VisitObject(this, state);

    bool IObjectTypeShape.HasProperties => CreatePropertiesFunc is not null;
    bool IObjectTypeShape.HasConstructor => CreateConstructorFunc is not null;

    IEnumerable<IPropertyShape> IObjectTypeShape.GetProperties() =>
        CreatePropertiesFunc?.Invoke() ?? [];

    IConstructorShape? IObjectTypeShape.GetConstructor() =>
        CreateConstructorFunc?.Invoke();
}
