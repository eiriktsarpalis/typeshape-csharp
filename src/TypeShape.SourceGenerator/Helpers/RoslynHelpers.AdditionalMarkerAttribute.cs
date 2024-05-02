using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;


namespace TypeShape.SourceGenerator.Helpers;

internal static partial class RoslynHelpers
{
    public static void RegisterAdditionalMarkerAttributeName(this IncrementalGeneratorInitializationContext context,
        MarkerAttributeHolder holder)
    {
        var valueProvider = context.AnalyzerConfigOptionsProvider
            .Select((options, _) =>
            {
                if (options.GlobalOptions.TryGetValue(
                        "build_property.TypeShape_SourceGenerator_AdditionalMarkerAttributeName",
                        out var markerAttribute))
                {
                    NameSyntax attributeNameSyntax = SyntaxFactory.ParseName(markerAttribute);
                    string attributeName = GetTypeName(attributeNameSyntax, out int attributeArity);
                    ImmutableArray<string> attributeNamespace = GetNamespaceTokens(attributeNameSyntax);
                    string? attributeNameMinusSuffix = attributeName.EndsWith("Attribute", StringComparison.Ordinal)
                        ? attributeName[..^"Attribute".Length]
                        : null;

                    holder.Populate(attributeName, attributeNameMinusSuffix, attributeNamespace, attributeArity);
                }

                return 0;
            });
        context.RegisterSourceOutput(valueProvider, (_, _) => { }); // this does not produce anything, but is required for the provider to be executed?

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

    public static IncrementalValuesProvider<TypeWithAttributeDeclarationContext>
        ForTypesWithOptionalAttributeDeclaration(
            this SyntaxValueProvider provider, MarkerAttributeHolder holder,
            Func<BaseTypeDeclarationSyntax, CancellationToken, bool> predicate)
    {
        return provider.CreateSyntaxProvider(
                predicate: (SyntaxNode node, CancellationToken token) => node is BaseTypeDeclarationSyntax typeDecl &&
                                                                         IsAnnotatedTypeDeclaration(typeDecl, token),
                transform: Transform)
            .Where(ctx => ctx.Type != null)
            .GroupBy(
                keySelector: value => value.Type,
                resultSelector: static (key, values) =>
                    new TypeWithAttributeDeclarationContext
                    {
                        TypeSymbol = (ITypeSymbol) key!,
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
                    if ((name == holder.AttributeName || name == holder.AttributeNameMinusSuffix) &&
                        arity == holder.AttributeArity)
                    {
                        return predicate(typeDecl, token);
                    }
                }
            }

            return false;
        }

        (ITypeSymbol Type, BaseTypeDeclarationSyntax Syntax, SemanticModel Model) Transform(GeneratorSyntaxContext ctx,
            CancellationToken token)
        {
            BaseTypeDeclarationSyntax typeDecl = (BaseTypeDeclarationSyntax) ctx.Node;
            ITypeSymbol typeSymbol = ctx.SemanticModel.GetDeclaredSymbol(typeDecl, token)!;

            foreach (AttributeData attrData in typeSymbol.GetAttributes())
            {
                if (attrData.AttributeClass is INamedTypeSymbol attributeType &&
                    attributeType.Name == holder.AttributeName &&
                    attributeType.Arity == holder.AttributeArity &&
                    attributeType.ContainingNamespace.MatchesNamespace(holder.AttributeNamespace))
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
    }

    internal sealed record MarkerAttributeHolder
    {
        public string AttributeName { get; private set; } = default!;
        public string? AttributeNameMinusSuffix { get; private set; }
        public ImmutableArray<string> AttributeNamespace { get; private set; }
        public int AttributeArity { get; private set; }

        public void Populate(string attributeName, string? attributeNameMinusSuffix,
            ImmutableArray<string> attributeNamespace, int attributeArity)
        {
            AttributeName = attributeName;
            AttributeNameMinusSuffix = attributeNameMinusSuffix;
            AttributeNamespace = attributeNamespace;
            AttributeArity = attributeArity;
        }
    }

    public static IncrementalValuesProvider<TSource> Concat<TSource>(this IncrementalValuesProvider<TSource> left,
        IncrementalValuesProvider<TSource> right)
    {
        return left.Collect().Combine(right.Collect()).SelectMany((tuple, _) =>
            ImmutableArray<TSource>.Empty.AddRange(tuple.Left.Concat(tuple.Right)));
    }
}