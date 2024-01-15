using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;

namespace TypeShape.SourceGenerator.Helpers;

internal static partial class RoslynHelpers
{
    /// <summary>
    /// Removes erased compiler metadata such as tuple names and nullable annotations.
    /// </summary>
    public static ITypeSymbol EraseCompilerMetadata(this Compilation compilation, ITypeSymbol type)
    {
        if (type.NullableAnnotation != NullableAnnotation.None)
        {
            type = type.WithNullableAnnotation(NullableAnnotation.None);
        }

        if (type is INamedTypeSymbol namedType)
        {
            if (namedType.IsTupleType)
            {
                if (namedType.TupleElements.Length < 2)
                {
                    return type;
                }

                ImmutableArray<ITypeSymbol> erasedElements = namedType.TupleElements
                    .Select(e => compilation.EraseCompilerMetadata(e.Type))
                    .ToImmutableArray();

                type = compilation.CreateTupleTypeSymbol(erasedElements);
            }
            else if (namedType.IsGenericType)
            {
                ImmutableArray<ITypeSymbol> typeArguments = namedType.TypeArguments;
                INamedTypeSymbol? containingType = namedType.ContainingType;

                if (containingType?.IsGenericType == true)
                {
                    containingType = (INamedTypeSymbol)compilation.EraseCompilerMetadata(containingType);
                    type = namedType = containingType.GetTypeMembers().First(t => t.Name == namedType.Name && t.Arity == namedType.Arity);
                }

                if (typeArguments.Length > 0)
                {
                    ITypeSymbol[] erasedTypeArgs = typeArguments
                        .Select(compilation.EraseCompilerMetadata)
                        .ToArray();

                    type = namedType.ConstructedFrom.Construct(erasedTypeArgs);
                }
            }
        }

        return type;
    }

    /// <summary>
    /// this.QualifiedNameOnly = containingSymbol.QualifiedNameOnly + "." + this.Name
    /// </summary>
    public static SymbolDisplayFormat QualifiedNameOnlyFormat { get; } = 
        new SymbolDisplayFormat(
            globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            memberOptions: SymbolDisplayMemberOptions.IncludeContainingType);

    public static string GetFullyQualifiedName(this ITypeSymbol typeSymbol)
        => typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    public static string GetFullyQualifiedName(this IMethodSymbol methodSymbol)
    {
        Debug.Assert(methodSymbol.IsStatic && methodSymbol.MethodKind is not MethodKind.Constructor);
        return $"{methodSymbol.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}.{methodSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}";
    }

    public static bool IsGenericTypeDefinition(this ITypeSymbol type)
        => type is INamedTypeSymbol { IsGenericType: true } namedType && 
           SymbolEqualityComparer.Default.Equals(namedType.OriginalDefinition, type);

    public static string GetGeneratedPropertyName(this ITypeSymbol type)
    {
        switch (type)
        { 
            case IArrayTypeSymbol arrayType:
                int rank = arrayType.Rank;
                string suffix = rank == 1 ? "_Array" : $"_Array{rank}D"; // Array, Array2D, Array3D, ...
                return arrayType.ElementType.GetGeneratedPropertyName() + suffix;

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
                Debug.Fail($"Type {type} not supported");
                return null!;
        }
    }

    public static Location? GetLocation(this AttributeData attributeData)
    {
        SyntaxReference? asr = attributeData.ApplicationSyntaxReference;
        return asr?.SyntaxTree.GetLocation(asr.Span);
    }

    /// <summary>
    /// Returns the kind keyword corresponding to the specified declaration syntax node.
    /// </summary>
    public static string GetTypeKindKeyword(this BaseTypeDeclarationSyntax typeDeclaration)
    {
        switch (typeDeclaration.Kind())
        {
            case SyntaxKind.ClassDeclaration:
                return "class";
            case SyntaxKind.InterfaceDeclaration:
                return "interface";
            case SyntaxKind.StructDeclaration:
                return "struct";
            case SyntaxKind.RecordDeclaration:
                return "record";
            case SyntaxKind.RecordStructDeclaration:
                return "record struct";
            case SyntaxKind.EnumDeclaration:
                return "enum";
            case SyntaxKind.DelegateDeclaration:
                return "delegate";
            default:
                Debug.Fail("unexpected syntax kind");
                return null!;
        }
    }
}
