namespace TypeShape.Roslyn;

/// <summary>
/// The construction strategy resolved for the collection type.
/// </summary>
public enum CollectionModelConstructionStrategy
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
    /// Type exposes a constructor or factory method that takes a parameter that is compatible with <see cref="List{T}"/>,
    /// such as <see cref="IEnumerable{T}"/> or <see cref="IList{T}"/>.
    /// </summary>
    List,

    /// <summary>
    /// Type exposes a constructor or factory method that takes a parameter that is compatible with <see cref="Dictionary{TKey, TValue}"/>,
    /// such as <see cref="IReadOnlyDictionary{TKey, TValue}"/> or <see cref="IDictionary{TKey, TValue}"/>.
    /// </summary>
    Dictionary,

    /// <summary>
    /// Type defines a constructor or factory method accepting a <see cref="ReadOnlySpan{T}"/>,
    /// typically declared via CollectionBuilderAttribute annotations.
    /// </summary>
    Span,
    
    /// <summary>
    /// Type defines a constructor or factory method accepting enumerable of
    /// <see cref="Tuple{T1, T2}"/> instead of <see cref="KeyValuePair{TKey,TValue}"/>,
    /// typically used by factories of the F# map type. 
    /// </summary>
    TupleEnumerable,
}
