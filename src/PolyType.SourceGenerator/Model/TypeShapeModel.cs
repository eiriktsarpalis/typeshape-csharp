using PolyType.Roslyn;

namespace PolyType.SourceGenerator.Model;

public abstract record TypeShapeModel
{
    public required TypeId Type { get; init; }

    /// <summary>
    /// A unique identifier corresponding to the type that is a valid C# identifier.
    /// </summary>
    public required string SourceIdentifier { get; init; }
}
