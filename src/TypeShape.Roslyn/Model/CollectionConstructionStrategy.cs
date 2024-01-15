namespace TypeShape.Roslyn;

/// <summary>
/// The construction strategy resolved for the collection type.
/// </summary>
public enum CollectionConstructionStrategy
{
    /// <summary>
    /// No available construction strategy.
    /// </summary>
    None = 0,

    /// <summary>
    /// Type exposes a default constructor and an Add method accepting the element type
    /// or a has a settable indexer for the case of dictionaries.
    /// </summary>
    Mutable,

    /// <summary>
    /// Type exposes a constructor or factory method that takes a <see cref="IEnumerable{T}"/>.
    /// </summary>
    Enumerable,

    /// <summary>
    /// Type defines a constructor or factory method accepting a <see cref="ReadOnlySpan{T}"/>,
    /// typically declared via CollectionBuilderAttribute annotations.
    /// </summary>
    Span,
}
