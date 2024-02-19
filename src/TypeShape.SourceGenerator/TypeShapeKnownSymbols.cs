using Microsoft.CodeAnalysis;
using TypeShape.Roslyn;

namespace TypeShape.SourceGenerator;

public sealed class TypeShapeKnownSymbols(Compilation compilation) : KnownSymbols(compilation)
{
    public INamedTypeSymbol? GenerateShapeAttributeOfT => GetOrResolveType("TypeShape.GenerateShapeAttribute`1", ref _GenerateShapeAttributeOfT);
    private Option<INamedTypeSymbol?> _GenerateShapeAttributeOfT;

    public INamedTypeSymbol? PropertyShapeAttribute => GetOrResolveType("TypeShape.PropertyShapeAttribute", ref _PropertyShapeAttribute);
    private Option<INamedTypeSymbol?> _PropertyShapeAttribute;

    public INamedTypeSymbol? ConstructorShapeAttribute => GetOrResolveType("TypeShape.ConstructorShapeAttribute", ref _ConstructorShapeAttribute);
    private Option<INamedTypeSymbol?> _ConstructorShapeAttribute;

    public INamedTypeSymbol? ParameterShapeAttribute => GetOrResolveType("TypeShape.ParameterShapeAttribute", ref _ParameterShapeAttribute);
    private Option<INamedTypeSymbol?> _ParameterShapeAttribute;
}
