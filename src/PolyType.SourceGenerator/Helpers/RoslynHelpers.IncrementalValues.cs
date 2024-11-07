using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Diagnostics;

namespace PolyType.SourceGenerator.Helpers;

public readonly struct TypeWithAttributeDeclarationContext
{
    public required ITypeSymbol TypeSymbol { get; init; }
    public required ImmutableArray<(BaseTypeDeclarationSyntax Syntax, SemanticModel Model)> Declarations { get; init; }
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
        NameSyntax attributeNameSyntax = SyntaxFactory.ParseName(attributeFullyQualifiedName);
        string attributeName = GetTypeName(attributeNameSyntax, out int attributeArity);
        ImmutableArray<string> attributeNamespace = GetNamespaceTokens(attributeNameSyntax);
        string? attributeNameMinusSuffix = attributeName.EndsWith("Attribute", StringComparison.Ordinal) ? attributeName[..^"Attribute".Length] : null;

        return provider.CreateSyntaxProvider(
            predicate: (SyntaxNode node, CancellationToken token) => node is BaseTypeDeclarationSyntax typeDecl && IsAnnotatedTypeDeclaration(typeDecl, token),
            transform: Transform)
            .Where(ctx => ctx.Type != null)
            .GroupBy(
                keySelector: value => value.Type, 
                resultSelector: static (key, values) => 
                    new TypeWithAttributeDeclarationContext 
                    { 
                        TypeSymbol = (ITypeSymbol)key!, 
                        Declarations = values.Select(v => (v.Syntax, v.Model)).ToImmutableArray() 
                    },

                keyComparer: SymbolEqualityComparer.Default);

        bool IsAnnotatedTypeDeclaration(BaseTypeDeclarationSyntax typeDecl, CancellationToken token)
        {
            foreach (AttributeListSyntax attributeList in typeDecl.AttributeLists)
            {
                foreach (AttributeSyntax attribute in attributeList.Attributes)
                {
                    string name = GetTypeName(attribute.Name, out int arity);
                    if ((name == attributeName || name == attributeNameMinusSuffix) && arity == attributeArity)
                    {
                        return predicate(typeDecl, token);
                    }
                }
            }

            return false;
        }

        (ITypeSymbol Type, BaseTypeDeclarationSyntax Syntax, SemanticModel Model) Transform(GeneratorSyntaxContext ctx, CancellationToken token)
        {
            BaseTypeDeclarationSyntax typeDecl = (BaseTypeDeclarationSyntax)ctx.Node;
            ITypeSymbol typeSymbol = ctx.SemanticModel.GetDeclaredSymbol(typeDecl, token)!;

            foreach (AttributeData attrData in typeSymbol.GetAttributes())
            {
                if (attrData.AttributeClass is INamedTypeSymbol attributeType &&
                    attributeType.Name == attributeName &&
                    attributeType.Arity == attributeArity &&
                    attributeType.ContainingNamespace.MatchesNamespace(attributeNamespace))
                {
                    return (typeSymbol, typeDecl, ctx.SemanticModel);
                }
            }

            return default;
        }

        static string GetTypeName(NameSyntax nameSyntax, out int genericTypeArity)
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

        static ImmutableArray<string> GetNamespaceTokens(NameSyntax nameSyntax)
        {
            var tokens = new List<SimpleNameSyntax>();
            Traverse(nameSyntax);

            SimpleNameSyntax typeName = tokens[^1];
            return tokens.Select(t => t.Identifier.Text).Take(tokens.Count - 1).ToImmutableArray();

            void Traverse(NameSyntax current)
            {
                switch (current)
                {
                    case SimpleNameSyntax simpleName:
                        tokens.Add(simpleName);
                        break;
                    case QualifiedNameSyntax qualifiedName:
                        Traverse(qualifiedName.Left);
                        Traverse(qualifiedName.Right);
                        break;
                    case AliasQualifiedNameSyntax alias:
                        Traverse(alias.Name);
                        break;
                    default:
                        Debug.Fail("Unrecognized NameSyntax");
                        break;
                }
            }
        }
    }

    // Cf. https://github.com/dotnet/roslyn/issues/72667
    public static IncrementalValuesProvider<TResult> GroupBy<TSource, TKey, TResult>(
        this IncrementalValuesProvider<TSource> source,
        Func<TSource, TKey> keySelector,
        Func<TKey, IEnumerable<TSource>, TResult> resultSelector,
        IEqualityComparer<TKey>? keyComparer = null)
    {
        keyComparer ??= EqualityComparer<TKey>.Default;
        return source.Collect().SelectMany((values, _) => values.GroupBy(keySelector, resultSelector, keyComparer));
    }
}