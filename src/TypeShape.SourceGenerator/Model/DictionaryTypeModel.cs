namespace TypeShape.SourceGenerator.Model;

public enum DictionaryKind
{
    IDictionaryOfKV,
    IReadOnlyDictionaryOfKV,
    IDictionary,
}

public sealed record DictionaryTypeModel
{
    public required TypeId Type { get; init; }
    public required TypeId KeyType { get; init; }
    public required TypeId ValueType { get; init; }
    public required DictionaryKind Kind { get; init; }
    public required CollectionConstructionStrategy ConstructionStrategy { get; init; }
    public required bool HasSettableIndexer { get; init; }
    public required string? EnumerableFactoryMethod { get; init; }
    public required string? SpanFactoryMethod { get; init; }
}
