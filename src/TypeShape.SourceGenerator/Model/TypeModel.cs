using TypeShape.SourceGenerator.Helpers;

namespace TypeShape.SourceGenerator.Model;

public sealed record TypeModel
{
    public required TypeId Id { get; init; }
    
    public required ImmutableEquatableArray<PropertyModel> Properties { get; init; }
    public required ImmutableEquatableArray<ConstructorModel> Constructors { get; init; }
    public required EnumTypeModel? EnumType { get; init; }
    public required NullableTypeModel? NullableType { get; init; }
    public required EnumerableTypeModel? EnumerableType { get; init; }
    public required DictionaryTypeModel? DictionaryType { get; init; }
    public required bool IsValueTupleType { get; init; }
    public required bool IsClassTupleType { get; init; }
    public required bool IsRecord { get; init; }
    public required bool EmitGenericTypeShapeProviderImplementation { get; init; }
}
