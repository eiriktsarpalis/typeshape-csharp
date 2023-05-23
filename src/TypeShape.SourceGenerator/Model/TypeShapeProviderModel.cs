using TypeShape.SourceGenerator.Helpers;

namespace TypeShape.SourceGenerator.Model;

public sealed record TypeShapeProviderModel
{
    public required string Name { get; init; }
    public required string TypeDeclaration { get; init; }

    public required string? Namespace { get; init; }
    public required ImmutableEquatableArray<string> ContainingTypes { get; init; }

    public required ImmutableEquatableArray<TypeModel> ProvidedTypes { get; init; }
    public required ImmutableEquatableArray<DiagnosticInfo> Diagnostics { get; init; }
}
