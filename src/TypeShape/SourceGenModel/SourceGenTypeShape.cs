using System.Reflection;

namespace TypeShape.SourceGenModel;

/// <summary>
/// Source generator model for type shapes.
/// </summary>
/// <typeparam name="T">The type whose shape is described.</typeparam>
public sealed class SourceGenTypeShape<T> : ITypeShape<T>
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
    /// The custom attribute provider for the type.
    /// </summary>
    public ICustomAttributeProvider? AttributeProvider { get; init; }

    /// <summary>
    /// The factory method for creating property shapes.
    /// </summary>
    public Func<IEnumerable<IPropertyShape>>? CreatePropertiesFunc { get; init; }

    /// <summary>
    /// The factory method for creating constructor shapes.
    /// </summary>
    public Func<IEnumerable<IConstructorShape>>? CreateConstructorsFunc { get; init; }

    /// <summary>
    /// The factory method for creating a dictionary shape.
    /// </summary>
    public Func<IDictionaryShape>? CreateDictionaryShapeFunc { get; init; }

    /// <summary>
    /// The factory method for creating an enumerable shape.
    /// </summary>
    public Func<IEnumerableShape>? CreateEnumerableShapeFunc { get; init; }

    /// <summary>
    /// The factory method for creating an enum shape.
    /// </summary>
    public Func<IEnumShape>? CreateEnumShapeFunc { get; init; }

    /// <summary>
    /// The factory method for creating a nullable shape.
    /// </summary>
    public Func<INullableShape>? CreateNullableShapeFunc { get; init; }

    /// <summary>
    /// Gets the kind of the type.
    /// </summary>
    public TypeKind Kind => _kind ??= GetKind();
    private TypeKind? _kind;

    private TypeKind GetKind()
    {
        TypeKind kind = TypeKind.None;

        if (CreateEnumerableShapeFunc != null)
        {
            kind |= TypeKind.Enumerable;
        }

        if (CreateDictionaryShapeFunc != null)
        {
            kind |= TypeKind.Dictionary;
        }

        if (CreateNullableShapeFunc != null)
        {
            kind |= TypeKind.Nullable;
        }

        if (CreateEnumShapeFunc != null)
        {
            kind |= TypeKind.Enum;
        }

        if (CreatePropertiesFunc != null || CreateConstructorsFunc != null)
        {
            kind |= TypeKind.Object;
        }

        return kind;
    }

    IEnumerable<IConstructorShape> ITypeShape.GetConstructors()
        => CreateConstructorsFunc?.Invoke() ?? [];

    IEnumerable<IPropertyShape> ITypeShape.GetProperties()
        => CreatePropertiesFunc?.Invoke() ?? [];

    IEnumShape ITypeShape.GetEnumShape()
    {
        ValidateKind(TypeKind.Enum);
        return CreateEnumShapeFunc!();
    }

    INullableShape ITypeShape.GetNullableShape()
    {
        ValidateKind(TypeKind.Nullable);
        return CreateNullableShapeFunc!();
    }

    IEnumerableShape ITypeShape.GetEnumerableShape()
    {
        ValidateKind(TypeKind.Enumerable);
        return CreateEnumerableShapeFunc!();
    }

    IDictionaryShape ITypeShape.GetDictionaryShape()
    {
        ValidateKind(TypeKind.Dictionary);
        return CreateDictionaryShapeFunc!();
    }

    private void ValidateKind(TypeKind expectedKind)
    {
        if ((Kind & expectedKind) == 0)
        {
            throw new InvalidOperationException($"Type {typeof(T)} is not of kind {expectedKind}.");
        }
    }
}
