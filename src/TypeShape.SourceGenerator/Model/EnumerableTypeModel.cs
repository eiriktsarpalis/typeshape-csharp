namespace TypeShape.SourceGenerator.Model;

public enum EnumerableKind
{
    ArrayOfT,
    MultiDimensionalArrayOfT,
    IEnumerableOfT,
    ICollectionOfT,
    ImmutableOfT,
    IEnumerable,
    IList,
}

public sealed record EnumerableTypeModel
{
    public required TypeId Type { get; init; }
    public required TypeId ElementType { get; init; }
    public required EnumerableKind Kind { get; init; }
    public required int Rank { get; init; }
    public required string? AddElementMethod { get; init; }
}
