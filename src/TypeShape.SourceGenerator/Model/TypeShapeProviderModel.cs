using Microsoft.CodeAnalysis;
using TypeShape.SourceGenerator.Helpers;

namespace TypeShape.SourceGenerator.Model;

public sealed record TypeShapeProviderModel
{
    public required string Name { get; init; }
    public required string TypeDeclaration { get; init; }

    public required string? Namespace { get; init; }
    public required ImmutableArrayEq<string> ContainingTypes { get; init; }

    public required ImmutableArrayEq<TypeModel> ProvidedTypes { get; init; }
    public required ImmutableArrayEq<Diagnostic> Diagnostics { get; init; }
}
