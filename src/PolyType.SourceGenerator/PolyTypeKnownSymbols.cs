using Microsoft.CodeAnalysis;
using PolyType.Roslyn;

namespace PolyType.SourceGenerator;

public sealed class PolyTypeKnownSymbols(Compilation compilation) : KnownSymbols(compilation)
{
    public INamedTypeSymbol? GenerateShapeAttribute => GetOrResolveType("PolyType.GenerateShapeAttribute", ref _GenerateShapeAttribute);
    private Option<INamedTypeSymbol?> _GenerateShapeAttribute;

    public INamedTypeSymbol? GenerateShapeAttributeOfT => GetOrResolveType("PolyType.GenerateShapeAttribute`1", ref _GenerateShapeAttributeOfT);
    private Option<INamedTypeSymbol?> _GenerateShapeAttributeOfT;

    public INamedTypeSymbol? PropertyShapeAttribute => GetOrResolveType("PolyType.PropertyShapeAttribute", ref _PropertyShapeAttribute);
    private Option<INamedTypeSymbol?> _PropertyShapeAttribute;

    public INamedTypeSymbol? ConstructorShapeAttribute => GetOrResolveType("PolyType.ConstructorShapeAttribute", ref _ConstructorShapeAttribute);
    private Option<INamedTypeSymbol?> _ConstructorShapeAttribute;

    public INamedTypeSymbol? ParameterShapeAttribute => GetOrResolveType("PolyType.ParameterShapeAttribute", ref _ParameterShapeAttribute);
    private Option<INamedTypeSymbol?> _ParameterShapeAttribute;
}
