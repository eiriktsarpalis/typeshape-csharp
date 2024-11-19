namespace PolyType.Abstractions;

/// <summary>
/// Provides a strongly typed shape model for a .NET <see cref="Nullable{T}"/> type.
/// </summary>
public interface INullableTypeShape : ITypeShape
{
    /// <summary>
    /// The shape of the element type of the nullable.
    /// </summary>
    ITypeShape ElementType { get; }
}

/// <summary>
/// Provides a strongly typed shape model for a .NET <see cref="Nullable{T}"/> type.
/// </summary>
/// <typeparam name="T">The element type of the nullable.</typeparam>
public interface INullableTypeShape<T> : ITypeShape<T?>, INullableTypeShape
    where T : struct
{
    /// <summary>
    /// The shape of the element type of the nullable.
    /// </summary>
    new ITypeShape<T> ElementType { get; }
}