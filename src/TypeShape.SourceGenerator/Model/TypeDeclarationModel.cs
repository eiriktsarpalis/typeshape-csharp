using TypeShape.SourceGenerator.Helpers;

namespace TypeShape.SourceGenerator.Model;

public record TypeDeclarationModel
{
    public required TypeId Id { get; init; }
    public required string Name { get; init; }
    public required string TypeDeclarationHeader { get; init; }
    public required ImmutableEquatableArray<string> ContainingTypes { get; init; }
    public required string SourceFilenamePrefix { get; init; }
    public required string? Namespace { get; init; }
}
