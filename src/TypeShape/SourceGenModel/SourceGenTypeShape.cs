using TypeShape.Abstractions;

namespace TypeShape.SourceGenModel;

/// <summary>
/// Source generator model for object type shapes.
/// </summary>
/// <typeparam name="TObject">The type whose shape is described.</typeparam>
public sealed class SourceGenTypeShape<TObject> : ITypeShape<TObject>
{
    /// <summary>
    /// The provider that generated this shape.
    /// </summary>
    public required ITypeShapeProvider Provider { get; init; }

    /// <summary>
    /// Whether the type is a record.
    /// </summary>
    public required bool IsRecord { get; init; }

    /// <summary>
    /// The factory method for creating property shapes.
    /// </summary>
    public Func<IEnumerable<IPropertyShape>>? CreatePropertiesFunc { get; init; }

    /// <summary>
    /// The factory method for creating constructor shapes.
    /// </summary>
    public Func<IEnumerable<IConstructorShape>>? CreateConstructorsFunc { get; init; }

    IEnumerable<IPropertyShape> ITypeShape.GetProperties() =>
        CreatePropertiesFunc?.Invoke() ?? [];

    IEnumerable<IConstructorShape> ITypeShape.GetConstructors() =>
        CreateConstructorsFunc?.Invoke() ?? [];

    bool ITypeShape.HasProperties => CreatePropertiesFunc != null;
    bool ITypeShape.HasConstructors => CreateConstructorsFunc != null;
}
