using Microsoft.CodeAnalysis;

namespace TypeShape.SourceGenerator.Model;

public sealed class KnownSymbols
{
    public ITypeSymbol DelegateType { get; }
    public ITypeSymbol? MemberInfoType { get; }
    public ITypeSymbol? IReadOnlyDictionaryOfTKeyTValue { get; }
    public ITypeSymbol? IDictionaryOfTKeyTValue { get; }
    public ITypeSymbol? IDictionary { get; }
    public ITypeSymbol? IList { get; }

    public ITypeSymbol? ImmutableArray { get; }
    public ITypeSymbol? ImmutableList { get; }
    public ITypeSymbol? ImmutableQueue { get; }
    public ITypeSymbol? ImmutableStack { get; }
    public ITypeSymbol? ImmutableHashSet { get; }
    public ITypeSymbol? ImmutableSortedSet { get; }
    public ITypeSymbol? ImmutableDictionary { get; }
    public ITypeSymbol? ImmutableSortedDictionary { get; }

    public KnownSymbols(Compilation compilation)
    {
        DelegateType = compilation.GetSpecialType(SpecialType.System_Delegate);
        MemberInfoType = compilation.GetTypeByMetadataName("System.Reflection.MemberInfo");

        IReadOnlyDictionaryOfTKeyTValue = compilation.GetTypeByMetadataName("System.Collections.Generic.IReadOnlyDictionary`2");
        IDictionaryOfTKeyTValue = compilation.GetTypeByMetadataName("System.Collections.Generic.IDictionary`2");
        IDictionary = compilation.GetTypeByMetadataName("System.Collections.IDictionary");
        IList = compilation.GetTypeByMetadataName("System.Collections.IList");

        ImmutableArray = compilation.GetTypeByMetadataName("System.Collections.Immutable.ImmutableArray`1");
        ImmutableList = compilation.GetTypeByMetadataName("System.Collections.Immutable.ImmutableList`1");
        ImmutableQueue = compilation.GetTypeByMetadataName("System.Collections.Immutable.ImmutableQueue`1");
        ImmutableStack = compilation.GetTypeByMetadataName("System.Collections.Immutable.ImmutableStack`1");
        ImmutableHashSet = compilation.GetTypeByMetadataName("System.Collections.Immutable.ImmutableHashSet`1");
        ImmutableSortedSet = compilation.GetTypeByMetadataName("System.Collections.Immutable.ImmutableSortedSet`1");
        ImmutableDictionary = compilation.GetTypeByMetadataName("System.Collections.Immutable.ImmutableDictionary`2");
        ImmutableSortedDictionary = compilation.GetTypeByMetadataName("System.Collections.Immutable.ImmutableSortedDictionary`2");
    }
}
