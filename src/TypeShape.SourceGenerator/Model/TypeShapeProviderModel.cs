using TypeShape.SourceGenerator.Helpers;

namespace TypeShape.SourceGenerator.Model;

public sealed record TypeShapeProviderModel
{
    public required string Name { get; init; }
    public required string TypeDeclaration { get; init; }
    public required string SourceFilenamePrefix { get; init; }

    public required string? Namespace { get; init; }
    public required ImmutableEquatableArray<string> ContainingTypes { get; init; }
    public required ImmutableEquatableDictionary<TypeId, TypeModel> ProvidedTypes { get; init; }
    public required ImmutableEquatableSet<DiagnosticInfo> Diagnostics { get; init; }
}