namespace TypeShape;

/// <summary>
/// The construction strategy use for a given <see cref="IEnumerableShape"/> or <see cref="IDictionaryShape"/>.
/// </summary>
[Flags]
public enum CollectionConstructionStrategy
{
    /// <summary>
    /// No known construction strategy for the current collection.
    /// </summary>
    None = 0,

    /// <summary>
    /// Constructed using a default constructor and an <see cref="ICollection{T}"/>-compatible Add method.
    /// </summary>
    Mutable = 1,

    /// <summary>
    /// Constructed using a <see cref="SpanConstructor{T, TDeclaringType}"/> delegate.
    /// </summary>
    Span = 2,

    /// <summary>
    /// Constructed using a <see cref="Func{TEnumerable, TDeclaringType}"/> delegate.
    /// </summary>
    Enumerable = 4,
}
