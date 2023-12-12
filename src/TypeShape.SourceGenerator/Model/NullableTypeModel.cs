namespace TypeShape.SourceGenerator.Model;

public sealed record NullableTypeModel
{
    public required TypeId ElementType { get; init; }
}
