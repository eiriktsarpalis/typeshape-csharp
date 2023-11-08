using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Diagnostics;

namespace TypeShape.SourceGenerator.Helpers;

public readonly struct TypeWithAttributeDeclarationContext
{
    public required BaseTypeDeclarationSyntax DeclarationSyntax { get; init; }
    public required ITypeSymbol TypeSymbol { get; init; }
    public required SemanticModel SemanticModel { get; init; }
}

internal static partial class RoslynHelpers
{
    /// <summary>
    /// Replacement for <see cref="SyntaxValueProvider.ForAttributeWithMetadataName" /> that handles generic attributes correctly.
    /// </summary>
    public static IncrementalValuesProvider<TypeWithAttributeDeclarationContext> ForTypesWithAttributeDeclaration(
        this SyntaxValueProvider provider, string attributeFullyQualifiedName,
        Func<BaseTypeDeclarationSyntax, CancellationToken, bool> predicate)
    {
        string attributeName = ParseTypeName(SyntaxFactory.ParseName(attributeFullyQualifiedName), out int attributeArity);
        string? attributeNameMinusSuffix = attributeName.EndsWith("Attribute", StringComparison.Ordinal) ? attributeName[..^"Attribute".Length] : null;
        string? attributeNamespace = attributeFullyQualifiedName.LastIndexOf('.') is >= 0 and int i ? attributeFullyQualifiedName[..i] : null;

        return provider.CreateSyntaxProvider(
            predicate: (SyntaxNode node, CancellationToken token) => node is BaseTypeDeclarationSyntax typeDecl && IsAnnotatedTypeDeclaration(typeDecl, token),
            transform: Transform)
            .Where(ctx => ctx.DeclarationSyntax != null);

        bool IsAnnotatedTypeDeclaration(BaseTypeDeclarationSyntax typeDecl, CancellationToken token)
        {
            foreach (AttributeListSyntax attributeList in typeDecl.AttributeLists)
            {
                foreach (AttributeSyntax attribute in attributeList.Attributes)
                {
                    string name = ParseTypeName(attribute.Name, out int arity);
                    if ((name == attributeName || name == attributeNameMinusSuffix) && arity == attributeArity)
                    {
                        return predicate(typeDecl, token);
                    }
                }
            }

            return false;
        }

        TypeWithAttributeDeclarationContext Transform(GeneratorSyntaxContext ctx, CancellationToken token)
        {
            BaseTypeDeclarationSyntax typeDecl = (BaseTypeDeclarationSyntax)ctx.Node;
            ITypeSymbol typeSymbol = ctx.SemanticModel.GetDeclaredSymbol(typeDecl, token)!;

            foreach (AttributeData attrData in typeSymbol.GetAttributes())
            {
                if (attrData.AttributeClass is INamedTypeSymbol attributeType &&
                    attributeType.Name == attributeName &&
                    attributeType.Arity == attributeArity &&
                    attributeType.ContainingNamespace?.ToDisplayString() == attributeNamespace)
                {
                    return new() { SemanticModel = ctx.SemanticModel, DeclarationSyntax = typeDecl, TypeSymbol = typeSymbol };
                }
            }

            return default;
        }

        static string ParseTypeName(NameSyntax nameSyntax, out int genericTypeArity)
        {
            while (true)
            {
                switch (nameSyntax)
                {
                    case IdentifierNameSyntax id:
                        genericTypeArity = 0;
                        return id.Identifier.ValueText;
                    case GenericNameSyntax gn:
                        genericTypeArity = gn.Arity;
                        return gn.Identifier.ValueText;
                    case QualifiedNameSyntax qn:
                        nameSyntax = qn.Right;
                        continue;
                    case AliasQualifiedNameSyntax aqn:
                        nameSyntax = aqn.Name;
                        continue;
                    default:
                        Debug.Fail("Unrecognized NameSyntax");
                        break;
                }
            }
        }
    }
}