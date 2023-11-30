using Microsoft.CodeAnalysis;
using TypeShape.SourceGenerator.Helpers;

namespace TypeShape.SourceGenerator.Model;

public sealed record ConstructorModel
{
    public required TypeId DeclaringType { get; init; }
    public required bool IsPublic { get; init; }
    public required ImmutableEquatableArray<ConstructorParameterModel> Parameters { get; init; }
    public required ImmutableEquatableArray<ConstructorParameterModel> MemberInitializers { get; init; }
    public required string? StaticFactoryName { get; init; }

    public int TotalArity => Parameters.Length + MemberInitializers.Length;
    public bool IsStaticFactory => StaticFactoryName != null;
}

public sealed record ConstructorParameterModel
{
    public required string Name { get; init; }
    public required TypeId ParameterType { get; init; }
    public required int Position { get; init; }
    public required bool IsRequired { get; init; }
    public required bool IsNonNullable { get; init; }
    public required bool IsMemberInitializer { get; init; }
    public required bool HasDefaultValue { get; init; }
    public required object? DefaultValue { get; init; }
    public required bool DefaultValueRequiresCast { get; init; }
}
