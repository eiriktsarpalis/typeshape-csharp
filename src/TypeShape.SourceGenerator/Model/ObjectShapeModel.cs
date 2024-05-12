using TypeShape.Roslyn;

namespace TypeShape.SourceGenerator.Model;

public sealed record ObjectShapeModel : TypeShapeModel
{
    public required ImmutableEquatableArray<PropertyShapeModel> Properties { get; init; }
    public required ConstructorShapeModel? Constructor { get; init; }
    public required bool IsValueTupleType { get; init; }
    public required bool IsTupleType { get; init; }
    public required bool IsRecordType { get; init; }
}
