namespace TypeShape.SourceGenerator.Model;

public sealed record EnumTypeModel
{
    public required TypeId Type { get; init; }
    public required TypeId UnderlyingType { get; init; }
}
