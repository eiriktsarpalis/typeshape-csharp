using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace PolyType.SourceGenerator.Helpers;

internal static partial class RoslynHelpers
{
    public static ITypeSymbol GetMemberType(this ISymbol memberSymbol)
    {
        Debug.Assert(memberSymbol is IPropertySymbol or IFieldSymbol);
        return memberSymbol switch
        {
            IPropertySymbol p => p.Type,
            _ => ((IFieldSymbol)memberSymbol).Type,
        };
    }
    
    /// <summary>
    /// Removes erased compiler metadata such as tuple names and nullable annotations.
    /// </summary>
    public static ITypeSymbol EraseCompilerMetadata(this Compilation compilation, ITypeSymbol type)
    {
        if (type.NullableAnnotation != NullableAnnotation.None)
        {
            type = type.WithNullableAnnotation(NullableAnnotation.None);
        }

        if (type is IArrayTypeSymbol arrayType)
        {
            ITypeSymbol elementType = compilation.EraseCompilerMetadata(arrayType.ElementType);
            return compilation.CreateArrayTypeSymbol(elementType, arrayType.Rank);
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

    public static bool ContainsNullabilityAnnotations(this ITypeSymbol type)
    {
        if (type.NullableAnnotation is NullableAnnotation.Annotated)
        {
            return true;
        }

        if (type is IArrayTypeSymbol arrayType)
        {
            return arrayType.ElementType.ContainsNullabilityAnnotations();
        }
        
        if (type is INamedTypeSymbol namedType)
        {
            if (namedType.ContainingType?.ContainsNullabilityAnnotations() is true)
            {
                return true;
            }

            if (namedType.TypeArguments.Length > 0)
            {
                return namedType.TypeArguments.Any(t => t.ContainsNullabilityAnnotations());
            }
        }

        return false;
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

    public static IPropertySymbol GetBaseProperty(this IPropertySymbol property)
    {
        while (property.OverriddenProperty is { } baseProp)
        {
            property = baseProp;
        }

        return property;
    }

    public static bool MatchesNamespace(this ISymbol? symbol, ImmutableArray<string> namespaceTokens)
    {
        for (int i = namespaceTokens.Length - 1; i >= 0; i--)
        {
            if (symbol?.Name != namespaceTokens[i])
            {
                return false;
            }

            symbol = symbol.ContainingNamespace;
        }

        return symbol is null or INamespaceSymbol { IsGlobalNamespace: true };
    }

    /// <summary>
    /// Returns a string representation of the type suitable for use as an identifier in source.
    /// </summary>
    public static string CreateTypeIdentifier(this ITypeSymbol type)
    {
        StringBuilder sb = new();
        GenerateCore(type, sb);
        return sb.ToString();

        static void GenerateCore(ITypeSymbol type, StringBuilder sb)
        {
            switch (type)
            {
                case ITypeParameterSymbol typeParameter:
                    AppendAsPascalCase(typeParameter.Name);
                    break;

                case IArrayTypeSymbol arrayType:
                    GenerateCore(arrayType.ElementType, sb);
                    sb.Append("_Array");
                    if (arrayType.Rank > 1)
                    {
                        // _Array2D, _Array3D, etc.
                        sb.Append(arrayType.Rank);
                        sb.Append('D');
                    }
                    break;

                case INamedTypeSymbol namedType:
                    PrependContainingTypes(namedType);
                    AppendAsPascalCase(namedType.Name);

                    IEnumerable<ITypeSymbol> typeArguments = namedType.IsTupleType
                        ? namedType.TupleElements.Select(e => e.Type)
                        : namedType.TypeArguments;

                    foreach (ITypeSymbol argument in namedType.TypeArguments)
                    {
                        sb.Append('_');
                        GenerateCore(argument, sb);
                    }

                    break;

                default:
                    Debug.Fail($"Type {type} not supported");
                    throw new InvalidOperationException();
            }

            void PrependContainingTypes(INamedTypeSymbol namedType)
            {
                if (namedType.ContainingType is { } parent)
                {
                    PrependContainingTypes(parent);
                    GenerateCore(parent, sb);
                    sb.Append('_');
                }
            }

            void AppendAsPascalCase(string name)
            {
                // Avoid creating identifiers that are C# keywords
                Debug.Assert(name.Length > 0);
                sb.Append(char.ToUpperInvariant(name[0]));
                sb.Append(name, 1, name.Length - 1);
            }
        }
    }

    public static bool IsCSharpKeyword(string name) =>
        SyntaxFacts.GetKeywordKind(name) is not SyntaxKind.None ||
        SyntaxFacts.GetContextualKeywordKind(name) is not SyntaxKind.None;

    public static string EscapeKeywordIdentifier(string name) =>
        IsCSharpKeyword(name) ? "@" + name : name;

    public static Location? GetLocation(this AttributeData attributeData)
    {
        SyntaxReference? asr = attributeData.ApplicationSyntaxReference;
        return asr?.SyntaxTree.GetLocation(asr.Span);
    }

    public static AttributeData? GetAttribute(this ISymbol symbol, INamedTypeSymbol? attributeType, bool inherit = true)
    {
        if (attributeType is null)
        {
            return null;
        }

        AttributeData? attribute = symbol.GetAttributes()
            .FirstOrDefault(ad => SymbolEqualityComparer.Default.Equals(ad.AttributeClass, attributeType));

        if (attribute is null && inherit)
        {
            Debug.Assert(attribute is not IEventSymbol);

            if (symbol is IPropertySymbol { OverriddenProperty: { } baseProperty })
            {
                return baseProperty.GetAttribute(attributeType, inherit: true);
            }

            if (symbol is ITypeSymbol { BaseType: { } baseType })
            {
                return baseType.GetAttribute(attributeType, inherit);
            }

            if (symbol is IMethodSymbol { OverriddenMethod: { } baseMethod })
            {
                return baseMethod.GetAttribute(attributeType, inherit: true);
            }
        }

        return attribute;
    }

    public static bool HasAttribute(this ISymbol symbol, INamedTypeSymbol? attributeType)
        => attributeType != null && symbol.GetAttributes().Any(ad => SymbolEqualityComparer.Default.Equals(ad.AttributeClass, attributeType));

    public static bool TryGetNamedArgument<T>(this AttributeData attributeData, string name, [NotNullWhen(true)] out T? result)
    {
        foreach (KeyValuePair<string, TypedConstant> namedArg in attributeData.NamedArguments)
        {
            if (namedArg.Key == name)
            {
                result = (T)namedArg.Value.Value!;
                return true;
            }
        }

        result = default;
        return false;
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
