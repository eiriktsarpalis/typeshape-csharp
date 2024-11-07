using Microsoft.CodeAnalysis;

namespace PolyType.SourceGenerator.Model;

public sealed record ConstructorParameterShapeModel
{
    public required string Name { get; init; }
    public required string UnderlyingMemberName { get; init; }
    public required TypeId ParameterType { get; init; }
    public required TypeId DeclaringType { get; init; }
    public required ParameterKind Kind { get; init; }
    public required RefKind RefKind { get; init; }
    public required int Position { get; init; }
    public required bool IsRequired { get; init; }
    public required bool IsInitOnlyProperty { get; init; }
    public required bool IsNonNullable { get; init; }
    public required bool IsAccessible { get; init; }
    public required bool ParameterTypeContainsNullabilityAnnotations { get; init; }
    public required bool IsPublic { get; init; }
    public required bool IsField { get; init; }
    public required bool HasDefaultValue { get; init; }
    public required string? DefaultValueExpr { get; init; }
}

public enum ParameterKind
{
    ConstructorParameter,
    RequiredMember,
    OptionalMember
}