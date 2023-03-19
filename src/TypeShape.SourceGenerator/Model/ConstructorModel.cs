using TypeShape.SourceGenerator.Helpers;

namespace TypeShape.SourceGenerator.Model;

public sealed record ConstructorModel
{
    public required TypeId DeclaringType { get; init; }
    public required ImmutableArrayEq<ConstructorParameterModel> Parameters { get; init; }
    public required ImmutableArrayEq<ConstructorParameterModel> MemberInitializers { get; init; }

    public int TotalArity => Parameters.Count + MemberInitializers.Count;
}

public sealed record ConstructorParameterModel
{
    public required string Name { get; init; }
    public required TypeId ParameterType { get; init; }
    public required int Position { get; init; }
    public required bool IsRequired { get; init; }
    public required bool IsMemberInitializer { get; init; }
    public required bool IsAutoProperty { get; init; }
    public required bool HasDefaultValue { get; init; }
    public required object? DefaultValue { get; init; }
    public required bool DefaultValueRequiresCast { get; init; }
}
