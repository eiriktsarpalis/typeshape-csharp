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
    public static IncrementalValuesProvider<TypeWithAttributeDeclarationContext> ForTypesWithAttributeDeclarations(
        this SyntaxValueProvider provider,
        string[] attributeFullyQualifiedNames,
        Func<BaseTypeDeclarationSyntax, CancellationToken, bool> predicate)
    {
        Debug.Assert(attributeFullyQualifiedNames.Length is > 0 and <= 3, "Does not optimize for large lists of attributes.");
        ParseAttributeFullyQualifiedNames(attributeFullyQualifiedNames, out var attributeData, out var attributeSyntaxNodeCandidates);

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
                    foreach (var candidate in attributeSyntaxNodeCandidates)
                    {
                        if (candidate.name == name && candidate.arity == arity)
                        {
                            return predicate(typeDecl, token);
                        }
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
                if (attrData.AttributeClass is INamedTypeSymbol attributeType)
                {
                    foreach (var (attributeNamespace, attributeName, attributeArity) in attributeData)
                    {
                        if (attributeType.Name == attributeName &&
                            attributeType.Arity == attributeArity &&
                            attributeType.ContainingNamespace.MatchesNamespace(attributeNamespace))
                        {
                            return (typeSymbol, typeDecl, ctx.SemanticModel);
                        }
                    }
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

        static void ParseAttributeFullyQualifiedNames(
            string[] attributeFullyQualifiedNames,
            out (ImmutableArray<string> Namespace, string Name, int Arity)[] attributeData,
            out (string name, int arity)[] attributeSyntaxNodeCandidates)
        {
            attributeData = new (ImmutableArray<string> Namespace, string Name, int Arity)[attributeFullyQualifiedNames.Length];
            List<(string name, int arity)> attributeSyntaxNodeCandidateList = new();
            int i = 0;

            foreach (string attributeFqn in attributeFullyQualifiedNames)
            {
                NameSyntax attributeNameSyntax = SyntaxFactory.ParseName(attributeFqn);
                string attributeName = GetTypeName(attributeNameSyntax, out int attributeArity);

                attributeSyntaxNodeCandidateList.Add((attributeName, attributeArity));
                if (attributeName.EndsWith("Attribute", StringComparison.Ordinal))
                {
                    attributeSyntaxNodeCandidateList.Add((attributeName[..^"Attribute".Length], attributeArity));
                }

                attributeData[i++] = (GetNamespaceTokens(attributeNameSyntax), attributeName, attributeArity);
            }

            attributeSyntaxNodeCandidates = attributeSyntaxNodeCandidateList.ToArray();
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