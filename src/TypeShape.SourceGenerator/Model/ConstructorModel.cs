using TypeShape.SourceGenerator.Helpers;

namespace TypeShape.SourceGenerator.Model;

public sealed record ConstructorModel
{
    public required TypeId DeclaringType { get; init; }
    public required bool IsPublic { get; init; }
    public required ImmutableEquatableArray<ConstructorParameterModel> Parameters { get; init; }
    public required ImmutableEquatableArray<ConstructorParameterModel> RequiredOrInitMembers { get; init; }
    public required ImmutableEquatableArray<ConstructorParameterModel> OptionalMembers { get; init; }
    public required OptionalMemberFlagsType OptionalMemberFlagsType { get; init; }
    public required string? StaticFactoryName { get; init; }

    public int TotalArity => Parameters.Length + RequiredOrInitMembers.Length + OptionalMembers.Length;
    public bool IsStaticFactory => StaticFactoryName != null;
}

public enum OptionalMemberFlagsType
{
    None,
    Byte,
    UShort,
    UInt32,
    ULong,
    BitArray,
}