namespace TypeShape.SourceGenModel;

/// <summary>
/// Source generator model for nullable types.
/// </summary>
/// <typeparam name="T">The element type of the nullable type.</typeparam>
public sealed class SourceGenNullableShape<T> : INullableShape<T>
    where T : struct
{
    /// <summary>
    /// The shape of the element type.
    /// </summary>
    public required ITypeShape<T> ElementType { get; init; }
}
