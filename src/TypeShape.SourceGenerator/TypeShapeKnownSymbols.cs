using Microsoft.CodeAnalysis;
using TypeShape.Roslyn;

namespace TypeShape.SourceGenerator;

public sealed class TypeShapeKnownSymbols(Compilation compilation) : KnownSymbols(compilation)
{
    public INamedTypeSymbol? GenerateShapeAttributeOfT => GetOrResolveType("TypeShape.GenerateShapeAttribute`1", ref _GenerateShapeAttributeOfT);
    private Option<INamedTypeSymbol?> _GenerateShapeAttributeOfT;
}
