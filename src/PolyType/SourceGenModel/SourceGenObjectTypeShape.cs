using PolyType.Abstractions;

namespace PolyType.SourceGenModel;

/// <summary>
/// Source generator model for object type shapes.
/// </summary>
/// <typeparam name="TObject">The type whose shape is described.</typeparam>
public sealed class SourceGenObjectTypeShape<TObject> : IObjectTypeShape<TObject>
{
    /// <summary>
    /// The provider that generated this shape.
    /// </summary>
    public required ITypeShapeProvider Provider { get; init; }

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

    IEnumerable<IPropertyShape> IObjectTypeShape.GetProperties() =>
        CreatePropertiesFunc?.Invoke() ?? [];

    IConstructorShape? IObjectTypeShape.GetConstructor() =>
        CreateConstructorFunc?.Invoke();

    bool IObjectTypeShape.HasProperties => CreatePropertiesFunc != null;
    bool IObjectTypeShape.HasConstructor => CreateConstructorFunc != null;
}
