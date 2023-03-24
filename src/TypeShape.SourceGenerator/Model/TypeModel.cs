using TypeShape.SourceGenerator.Helpers;

namespace TypeShape.SourceGenerator.Model;

public sealed record TypeModel
{
    public required TypeId Id { get; init; }
    
    public required ImmutableArrayEq<PropertyModel> Properties { get; init; }
    public required ImmutableArrayEq<ConstructorModel> Constructors { get; init; }
    public required EnumTypeModel? EnumType { get; init; }
    public required NullableTypeModel? NullableType { get; init; }
    public required EnumerableTypeModel? EnumerableType { get; init; }
    public required DictionaryTypeModel? DictionaryType { get; init; }
    public required bool IsTupleType { get; init; }
}
