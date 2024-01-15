using Microsoft.CodeAnalysis;
using System.Collections;

namespace TypeShape.Roslyn;

/// <summary>
/// Dictionary data model for types implementing <see cref="IDictionary{TKey, TValue}"/>,
/// <see cref="IReadOnlyDictionary{TKey, TValue}"/> or <see cref="System.Collections.IDictionary"/>.
/// </summary>
public sealed class DictionaryDataModel : TypeDataModel
{
    public override TypeDataKind Kind => TypeDataKind.Dictionary;

    /// <summary>
    /// The type of key used by the dictionary. 
    /// Is <see cref="object"/> if implementing <see cref="IDictionary"/>.
    /// </summary>
    public required ITypeSymbol KeyType { get; init; }

    /// <summary>
    /// The type of value used by the dictionary. 
    /// Is <see cref="object"/> if implementing <see cref="IDictionary"/>.
    /// </summary>
    public required ITypeSymbol ValueType { get; init; }

    public required DictionaryKind DictionaryKind { get; init; }

    /// <summary>
    /// The preferred construction strategy for this collection type.
    /// </summary>
    public required CollectionConstructionStrategy ConstructionStrategy { get; init; }

    /// <summary>
    /// Static factory method accepting a <see cref="ReadOnlySpan{KeyValuePair{TKey, TValue}}"/>.
    /// </summary>
    /// <remarks>
    /// If <see langword="null"/> and <see cref="ConstructionStrategy"/> equals 
    /// <see cref="CollectionConstructionStrategy.Span"/>, then the type has an 
    /// accessible constructor accepting a <see cref="ReadOnlySpan{T}"/>.
    /// </remarks>
    public required IMethodSymbol? SpanFactory { get; init; }

    /// <summary>
    /// For collection interfaces, specifies a mutable implementation type.
    /// </summary>
    public required INamedTypeSymbol? ImplementationType { get; init; }

    /// <summary>
    /// Static factory method accepting an <see cref="IEnumerable{KeyValuePair{TKey, TValue}}"/>.
    /// </summary>
    /// <remarks>
    /// If <see langword="null"/> and <see cref="ConstructionStrategy"/> equals 
    /// <see cref="CollectionConstructionStrategy.Enumerable"/>, then the type has an 
    /// accessible constructor accepting a <see cref="IEnumerable{T}"/>.
    /// </remarks>
    public required IMethodSymbol? EnumerableFactory { get; init; }
}

public enum DictionaryKind
{
    /// <summary>
    /// The type implements <see cref="IDictionary{TKey, TValue}"/>.
    /// </summary>
    IDictionaryOfKV,

    /// <summary>
    /// The type implements <see cref="IReadOnlyDictionary{TKey, TValue}"/>.
    /// </summary>
    IReadOnlyDictionaryOfKV,

    /// <summary>
    /// The type implements <see cref="System.Collections.IDictionary"/>.
    /// </summary>
    IDictionary,
}