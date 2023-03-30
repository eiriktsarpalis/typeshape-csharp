using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;

namespace TypeShape.SourceGenerator.Helpers;

internal static class RoslynHelpers
{
    public static ITypeSymbol EraseCompilerMetadata(this Compilation compilation, ITypeSymbol type)
    {
        if (type is INamedTypeSymbol namedType)
        {
            if (type.IsTupleType)
            {
                if (namedType.TupleElements.Length < 2)
                {
                    return type;
                }

                ImmutableArray<ITypeSymbol> erasedElements = namedType.TupleElements
                    .Select(e => compilation.EraseCompilerMetadata(e.Type))
                    .ToImmutableArray();

                return compilation.CreateTupleTypeSymbol(erasedElements);
            }

            // TODO nullable reference type handling.
            // TODO type argument erasure
        }

        return type;
    }

    public static string GetFullyQualifiedName(this ITypeSymbol typeSymbol)
        => typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    public static string GetGeneratedPropertyName(this ITypeSymbol typeSymbol)
    {
        switch (typeSymbol)
        { 
            case IArrayTypeSymbol arrayTypeSymbol:
                int rank = arrayTypeSymbol.Rank;
                string suffix = rank == 1 ? "_Array" : $"_Array{rank}D"; // Array, Array2D, Array3D, ...
                return arrayTypeSymbol.ElementType.GetGeneratedPropertyName() + suffix;

            case INamedTypeSymbol namedType when (namedType.IsTupleType):
                {
                    StringBuilder sb = new();

                    sb.Append(namedType.Name);

                    foreach (IFieldSymbol element in namedType.TupleElements)
                    {
                        sb.Append('_');
                        sb.Append(element.Type.GetGeneratedPropertyName());
                    }

                    return sb.ToString();
                }

            case INamedTypeSymbol namedType:
                {
                    if (namedType.TypeArguments.Length == 0 && namedType.ContainingType is null)
                        return namedType.Name;

                    StringBuilder sb = new();

                    PrependContainingTypes(namedType);

                    sb.Append(namedType.Name);

                    foreach (ITypeSymbol argument in namedType.TypeArguments)
                    {
                        sb.Append('_');
                        sb.Append(argument.GetGeneratedPropertyName());
                    }

                    return sb.ToString();

                    void PrependContainingTypes(INamedTypeSymbol namedType)
                    {
                        if (namedType.ContainingType is { } parent)
                        {
                            PrependContainingTypes(parent);
                            sb.Append(parent.GetGeneratedPropertyName());
                            sb.Append('_');
                        }
                    }
                }

            default:
                Debug.Fail($"Type {typeSymbol} not supported");
                return null!;
        }
    }

    public static bool ContainsGenericParameters(this ITypeSymbol typeSymbol)
    {
        if (typeSymbol.TypeKind is TypeKind.TypeParameter or TypeKind.Error)
            return true;

        if (typeSymbol is INamedTypeSymbol namedTypeSymbol)
        {
            for (; namedTypeSymbol != null; namedTypeSymbol = namedTypeSymbol.ContainingType)
            {
                if (namedTypeSymbol.TypeArguments.Any(arg => arg.ContainsGenericParameters()))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public static bool IsNonTrivialTupleType(this ITypeSymbol typeSymbol)
    {
        return typeSymbol.IsTupleType && typeSymbol is INamedTypeSymbol ts && ts.TupleElements.Length > 1;
    }

    public static IEnumerable<IFieldSymbol> GetTupleElementsWithoutLabels(this INamedTypeSymbol tuple)
    {
        Debug.Assert(tuple.IsTupleType);

        foreach (IFieldSymbol element in tuple.TupleElements)
        {
            yield return element.IsExplicitlyNamedTupleElement ? element.CorrespondingTupleField! : element;
        }
    }

    public static bool IsAutoProperty(this IPropertySymbol property)
    {
        return property.ContainingType.GetMembers()
            .OfType<IFieldSymbol>()
            .Any(field => SymbolEqualityComparer.Default.Equals(field.AssociatedSymbol, property));
    }

    public static bool HasSetsRequiredMembersAttribute(this IMethodSymbol constructor)
    {
        Debug.Assert(constructor.MethodKind is MethodKind.Constructor);
        return constructor.GetAttributes().Any(attr => attr.AttributeClass?.GetFullyQualifiedName() == "global::System.Diagnostics.CodeAnalysis.SetsRequiredMembersAttribute");
    }

    /// <summary>
    /// Get a location object that doesn't capture a reference to Compilation.
    /// </summary>
    public static Location GetLocationTrimmed(this CSharpSyntaxNode node)
    {
        var location = node.GetLocation();
        return Location.Create(location.SourceTree?.FilePath ?? string.Empty, location.SourceSpan, location.GetLineSpan().Span);
    }

    public static INamedTypeSymbol[] GetSortedTypeHierarchy(this INamedTypeSymbol type)
    {
        if (type.TypeKind != TypeKind.Interface)
        {
            var list = new List<INamedTypeSymbol>();
            for (INamedTypeSymbol? current = type; current != null; current = current.BaseType)
            {
                list.Add(current);
            }

            return list.ToArray();
        }
        else
        {
            // Interface hierarchies support multiple inheritance.
            // For consistency with class hierarchy resolution order,
            // sort topologically from most derived to least derived.
            return CommonHelpers.TraverseGraphWithTopologicalSort<INamedTypeSymbol>(type, static t => t.AllInterfaces, SymbolEqualityComparer.Default);
        }
    }
}
