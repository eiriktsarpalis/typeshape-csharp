using Microsoft.CodeAnalysis;

namespace TypeShape.SourceGenerator.Model;

public sealed class KnownSymbols(Compilation compilation)
{
    public Compilation Compilation { get; } = compilation;

    public INamedTypeSymbol ObjectType => _ObjectType ??= Compilation.GetSpecialType(SpecialType.System_Object);
    private INamedTypeSymbol? _ObjectType;

    public INamedTypeSymbol? GenerateShapeAttribute => GetOrResolveType("TypeShape.GenerateShapeAttribute", ref _GenerateShapeAttribute);
    private Option<INamedTypeSymbol?> _GenerateShapeAttribute;

    public INamedTypeSymbol? GenerateShapeAttributeOfT => GetOrResolveType("TypeShape.GenerateShapeAttribute`1", ref _GenerateShapeAttributeOfT);
    private Option<INamedTypeSymbol?> _GenerateShapeAttributeOfT;

    public IAssemblySymbol CoreLibAssembly => _CoreLibAssembly ??= Compilation.GetSpecialType(SpecialType.System_Int32).ContainingAssembly;
    private IAssemblySymbol? _CoreLibAssembly;

    public INamedTypeSymbol DelegateType => _DelegateType ??= Compilation.GetSpecialType(SpecialType.System_Delegate);
    private INamedTypeSymbol? _DelegateType;

    public INamedTypeSymbol? MemberInfoType => GetOrResolveType("System.Reflection.MemberInfo", ref _MemberInfoType);
    private Option<INamedTypeSymbol?> _MemberInfoType;

    public INamedTypeSymbol? IReadOnlyDictionaryOfTKeyTValue => GetOrResolveType("System.Collections.Generic.IReadOnlyDictionary`2", ref _IReadOnlyDictionaryOfTKeyTValue);
    private Option<INamedTypeSymbol?> _IReadOnlyDictionaryOfTKeyTValue;

    public INamedTypeSymbol? IDictionaryOfTKeyTValue => GetOrResolveType("System.Collections.Generic.IDictionary`2", ref _IDictionaryOfTKeyTValue);
    private Option<INamedTypeSymbol?> _IDictionaryOfTKeyTValue;

    public INamedTypeSymbol? IDictionary => GetOrResolveType("System.Collections.IDictionary", ref _IDictionary);
    private Option<INamedTypeSymbol?> _IDictionary;

    public INamedTypeSymbol IEnumerableOfT => _IEnumerableOfT ??= Compilation.GetSpecialType(SpecialType.System_Collections_Generic_IEnumerable_T);
    private INamedTypeSymbol? _IEnumerableOfT;

    public INamedTypeSymbol ICollectionOfT => _ICollectionOfT ??= Compilation.GetSpecialType(SpecialType.System_Collections_Generic_ICollection_T);
    private INamedTypeSymbol? _ICollectionOfT;

    public INamedTypeSymbol IEnumerable => _IEnumerable ??= Compilation.GetSpecialType(SpecialType.System_Collections_IEnumerable);
    private INamedTypeSymbol? _IEnumerable;

    public INamedTypeSymbol? ReadOnlySpanOfT => GetOrResolveType("System.ReadOnlySpan`1", ref _ReadOnlySpanOfT);
    private Option<INamedTypeSymbol?> _ReadOnlySpanOfT;

    public INamedTypeSymbol? ListOfT => GetOrResolveType("System.Collections.Generic.List`1", ref _ListOfT);
    private Option<INamedTypeSymbol?> _ListOfT;

    public INamedTypeSymbol? HashSetOfT => GetOrResolveType("System.Collections.Generic.HashSet`1", ref _HashSetOfT);
    private Option<INamedTypeSymbol?> _HashSetOfT;

    public INamedTypeSymbol? KeyValuePairOfKV => GetOrResolveType("System.Collections.Generic.KeyValuePair`2", ref _KeyValuePairOfKV);
    private Option<INamedTypeSymbol?> _KeyValuePairOfKV;

    public INamedTypeSymbol? DictionaryOfTKeyTValue => GetOrResolveType("System.Collections.Generic.Dictionary`2", ref _DictionaryOfTKeyTValue);
    private Option<INamedTypeSymbol?> _DictionaryOfTKeyTValue;

    public INamedTypeSymbol? IList => GetOrResolveType("System.Collections.IList", ref _IList);
    private Option<INamedTypeSymbol?> _IList;

    public INamedTypeSymbol? ImmutableDictionary => GetOrResolveType("System.Collections.Immutable.ImmutableDictionary`2", ref _ImmutableDictionary);
    private Option<INamedTypeSymbol?> _ImmutableDictionary;

    public INamedTypeSymbol? ImmutableSortedDictionary => GetOrResolveType("System.Collections.Immutable.ImmutableSortedDictionary`2", ref _ImmutableSortedDictionary);
    private Option<INamedTypeSymbol?> _ImmutableSortedDictionary;

    private INamedTypeSymbol? GetOrResolveType(string fullyQualifiedName, ref Option<INamedTypeSymbol?> field)
    {
        if (field.HasValue)
        {
            return field.Value;
        }

        INamedTypeSymbol? type = Compilation.GetTypeByMetadataName(fullyQualifiedName);
        field = new(type);
        return type;
    }

    // An optional type that supports Some(null) representations.
    private readonly struct Option<T>
    {
        public readonly bool HasValue;
        public readonly T Value;

        public Option(T value)
        {
            HasValue = true;
            Value = value;
        }
    }
}
