using TypeShape.SourceGenerator.Helpers;

namespace TypeShape.SourceGenerator.Model;

public sealed record TypeShapeProviderModel
{
    public required TypeDeclarationModel Declaration { get; init; }
    public required ImmutableEquatableDictionary<TypeId, TypeModel> ProvidedTypes { get; init; }
    public required ImmutableEquatableSet<DiagnosticInfo> Diagnostics { get; init; }
    public required ImmutableEquatableArray<TypeDeclarationModel> GenerateShapeTypes { get; init; }
}