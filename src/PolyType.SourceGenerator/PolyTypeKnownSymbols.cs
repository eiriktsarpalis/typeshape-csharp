using Microsoft.CodeAnalysis;
using PolyType.Roslyn;

namespace PolyType.SourceGenerator;

public sealed class PolyTypeKnownSymbols(Compilation compilation) : KnownSymbols(compilation)
{
    public INamedTypeSymbol? GenerateShapeAttribute => GetOrResolveType("PolyType.GenerateShapeAttribute", ref _GenerateShapeAttribute);
    private Option<INamedTypeSymbol?> _GenerateShapeAttribute;

    public INamedTypeSymbol? GenerateShapeAttributeOfT => GetOrResolveType("PolyType.GenerateShapeAttribute`1", ref _GenerateShapeAttributeOfT);
    private Option<INamedTypeSymbol?> _GenerateShapeAttributeOfT;

    public INamedTypeSymbol? TypeShapeAttribute => GetOrResolveType("PolyType.TypeShapeAttribute", ref _TypeShapeAttribute);
    private Option<INamedTypeSymbol?> _TypeShapeAttribute;

    public INamedTypeSymbol? PropertyShapeAttribute => GetOrResolveType("PolyType.PropertyShapeAttribute", ref _PropertyShapeAttribute);
    private Option<INamedTypeSymbol?> _PropertyShapeAttribute;

    public INamedTypeSymbol? ConstructorShapeAttribute => GetOrResolveType("PolyType.ConstructorShapeAttribute", ref _ConstructorShapeAttribute);
    private Option<INamedTypeSymbol?> _ConstructorShapeAttribute;

    public INamedTypeSymbol? ParameterShapeAttribute => GetOrResolveType("PolyType.ParameterShapeAttribute", ref _ParameterShapeAttribute);
    private Option<INamedTypeSymbol?> _ParameterShapeAttribute;

    public TargetFramework TargetFramework => _targetFramework ??= ResolveTargetFramework();
    private TargetFramework? _targetFramework;

    private TargetFramework ResolveTargetFramework()
    {
        INamedTypeSymbol? alternateEqualityComparer = Compilation.GetTypeByMetadataName("System.Collections.Generic.IAlternateEqualityComparer`2");
        if (alternateEqualityComparer is not null &&
            SymbolEqualityComparer.Default.Equals(alternateEqualityComparer.ContainingAssembly, CoreLibAssembly))
        {
            return TargetFramework.Net90;
        }

        INamedTypeSymbol? searchValues = Compilation.GetTypeByMetadataName("System.Buffers.SearchValues");
        if (searchValues is not null &&
            SymbolEqualityComparer.Default.Equals(searchValues.ContainingAssembly, CoreLibAssembly))
        {
            return TargetFramework.Net80;
        }

        return TargetFramework.Legacy;
    }
}