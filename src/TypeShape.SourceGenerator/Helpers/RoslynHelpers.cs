using Microsoft.CodeAnalysis;
using System.Diagnostics;
using System.Text;

namespace TypeShape.SourceGenerator.Helpers;

internal static class RoslynHelpers
{
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

            case INamedTypeSymbol namedType:
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
}
