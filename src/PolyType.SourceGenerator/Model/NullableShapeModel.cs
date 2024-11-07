namespace PolyType.SourceGenerator.Model;

public sealed record NullableShapeModel : TypeShapeModel
{
    public required TypeId ElementType { get; init; }
}
