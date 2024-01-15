using TypeShape.Roslyn;

namespace TypeShape.SourceGenerator.Model;

public record TypeShapeModel
{
    public required TypeId Type { get; init; }
    public required bool EmitGenericTypeShapeProviderImplementation { get; init; }
}
