using TypeShape.Roslyn;

namespace TypeShape.SourceGenerator.Model;

public sealed record ObjectShapeModel : TypeShapeModel
{
    public required ImmutableEquatableArray<PropertyShapeModel> Properties { get; init; }
    public required ImmutableEquatableArray<ConstructorShapeModel> Constructors { get; init; }
    public required bool IsValueTupleType { get; init; }
    public required bool IsClassTupleType { get; init; }
    public required bool IsRecord { get; init; }
}
