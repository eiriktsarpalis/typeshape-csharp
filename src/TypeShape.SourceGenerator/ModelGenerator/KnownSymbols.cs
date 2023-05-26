using Microsoft.CodeAnalysis;

namespace TypeShape.SourceGenerator.Model;

public sealed class KnownSymbols
{
    public KnownSymbols(Compilation compilation)
    {
        Compilation = compilation;
    }

    public Compilation Compilation { get; }

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

    public INamedTypeSymbol? IList => GetOrResolveType("System.Collections.IList", ref _IList);
    private Option<INamedTypeSymbol?> _IList;

    public INamedTypeSymbol? ImmutableArray => GetOrResolveType("System.Collections.Immutable.ImmutableArray`1", ref _ImmutableArray);
    private Option<INamedTypeSymbol?> _ImmutableArray;

    public INamedTypeSymbol? ImmutableList => GetOrResolveType("System.Collections.Immutable.ImmutableList`1", ref _ImmutableList);
    private Option<INamedTypeSymbol?> _ImmutableList;

    public INamedTypeSymbol? ImmutableQueue => GetOrResolveType("System.Collections.Immutable.ImmutableQueue`1", ref _ImmutableQueue);
    private Option<INamedTypeSymbol?> _ImmutableQueue;

    public INamedTypeSymbol? ImmutableStack => GetOrResolveType("System.Collections.Immutable.ImmutableStack`1", ref _ImmutableStack);
    private Option<INamedTypeSymbol?> _ImmutableStack;

    public INamedTypeSymbol? ImmutableHashSet => GetOrResolveType("System.Collections.Immutable.ImmutableHashSet`1", ref _ImmutableHashSet);
    private Option<INamedTypeSymbol?> _ImmutableHashSet;

    public INamedTypeSymbol? ImmutableSortedSet => GetOrResolveType("System.Collections.Immutable.ImmutableSortedSet`1", ref _ImmutableSortedSet);
    private Option<INamedTypeSymbol?> _ImmutableSortedSet;

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
