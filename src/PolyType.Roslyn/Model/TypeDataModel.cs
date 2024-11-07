using Microsoft.CodeAnalysis;

namespace PolyType.Roslyn;

/// <summary>
/// Exposes a data model extracted from an <see cref="ITypeSymbol"/> input.
/// </summary>
public class TypeDataModel
{
    /// <summary>
    /// The <see cref="ITypeSymbol"/> that this model represents.
    /// </summary>
    public required ITypeSymbol Type { get; init; }

    /// <summary>
    /// Determines the type of <see cref="TypeDataModel"/> being used.
    /// </summary>
    public virtual TypeDataKind Kind => TypeDataKind.None;

    /// <summary>
    /// True if the type was explicitly passed to the <see cref="TypeDataModelGenerator"/>
    /// and is not a transitive dependency in the type graph.
    /// </summary>
    public bool IsRootType { get; internal set; }
}
