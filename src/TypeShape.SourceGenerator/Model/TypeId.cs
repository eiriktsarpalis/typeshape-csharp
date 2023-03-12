namespace TypeShape.SourceGenerator.Model;

public readonly record struct TypeId
{
    public required string FullyQualifiedName { get; init; }
    public required string GeneratedPropertyName { get; init; }
}