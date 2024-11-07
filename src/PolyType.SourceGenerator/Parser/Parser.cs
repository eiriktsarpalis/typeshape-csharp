using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;
using PolyType.Roslyn;
using PolyType.SourceGenerator.Helpers;
using PolyType.SourceGenerator.Model;

namespace PolyType.SourceGenerator;

public sealed partial class Parser : TypeDataModelGenerator
{
    private static readonly IEqualityComparer<(ITypeSymbol Type, string Name)> s_ctorParamComparer =
        CommonHelpers.CreateTupleComparer<ITypeSymbol, string>(
            SymbolEqualityComparer.Default,
            CommonHelpers.CamelCaseInvariantComparer.Instance);
    
    private readonly TypeShapeKnownSymbols _knownSymbols;

    // We want to flatten System.Tuple types for consistency with
    // the reflection-based provider (which caters to F# model types).
    protected override bool FlattenSystemTupleTypes => true;

    // All types used as generic parameters so we must exclude ref structs.
    protected override bool IsSupportedType(ITypeSymbol type)
        => base.IsSupportedType(type) && !type.IsRefLikeType;

    // Erase nullable annotations and tuple labels from generated types.
    protected override ITypeSymbol NormalizeType(ITypeSymbol type)
        => KnownSymbols.Compilation.EraseCompilerMetadata(type);

    // Ignore properties with the [PropertyShape] attribute set to Ignore = true.
    protected override bool IncludeProperty(IPropertySymbol property, out bool includeGetter, out bool includeSetter)
    {
        if (property.GetAttribute(_knownSymbols.PropertyShapeAttribute) is AttributeData propertyAttribute)
        {
            bool includeProperty = propertyAttribute.TryGetNamedArgument("Ignore", out bool ignoreValue) ? !ignoreValue : true;
            if (includeProperty)
            {
                // Use the signature of the base property to determine shape.
                property = property.GetBaseProperty();
                includeGetter = property.GetMethod is not null;
                includeSetter = property.SetMethod is not null;
                return true;
            }

            includeGetter = includeSetter = false;
            return false;
        }

        return base.IncludeProperty(property, out includeGetter, out includeSetter);
    }

    // Ignore fields with the [PropertyShape] attribute set to Ignore = true.
    protected override bool IncludeField(IFieldSymbol field)
    {
        if (field.GetAttribute(_knownSymbols.PropertyShapeAttribute) is AttributeData fieldAttribute)
        {
            return fieldAttribute.TryGetNamedArgument("Ignore", out bool ignoreValue) ? !ignoreValue : true;
        }

        return base.IncludeField(field);
    }

    // Resolve constructors with the [ConstructorShape] attribute.
    protected override IEnumerable<IMethodSymbol> ResolveConstructors(ITypeSymbol type, ImmutableArray<PropertyDataModel> properties)
    {
        // Search for constructors that have the [ConstructorShape] attribute. Ignore accessibility modifiers in this step.
        IMethodSymbol[] constructors = type.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(ctor => ctor is { IsStatic: false, MethodKind: MethodKind.Constructor })
            .Where(ctor => ctor.HasAttribute(_knownSymbols.ConstructorShapeAttribute))
            .ToArray();

        if (constructors.Length == 1)
        {
            return constructors; // Found a unique match, return that.
        }

        if (constructors.Length > 1)
        {
            // We have a conflict, report a diagnostic and pick one using the default heuristic.
            ReportDiagnostic(DuplicateConstructorShape, constructors[^1].Locations.FirstOrDefault(), type.ToDisplayString());
        }
        else
        {
            // Otherwise, just resolve the public constructors on the type.
            constructors = base.ResolveConstructors(type, properties)
                .Where(ctor => ctor.DeclaredAccessibility is Accessibility.Public)
                .ToArray();
        }

        // In case of ambiguity, return the constructor that maximizes
        // the number of parameters corresponding to read-only properties.
        HashSet<(ITypeSymbol, string)> readOnlyProperties = new(
            properties
                .Where(p => !p.IncludeSetter)
                .Select(p => (p.PropertyType, p.Name)), 
            s_ctorParamComparer);
            
        return constructors
            .OrderByDescending(ctor =>
            {
                int paramsMatchingReadOnlyMembers = ctor.Parameters.Count(p => readOnlyProperties.Contains((p.Type, p.Name)));
                // In case of a tie, pick the ctor with the smallest arity.
                return (paramsMatchingReadOnlyMembers, -ctor.Parameters.Length);
            })
            .Take(1);
    }

    private Parser(ISymbol generationScope, TypeShapeKnownSymbols knownSymbols, CancellationToken cancellationToken) 
        : base(generationScope, knownSymbols, cancellationToken)
    {
        _knownSymbols = knownSymbols;
    }

    public static TypeShapeProviderModel ParseFromGenerateShapeOfTAttributes(
        TypeWithAttributeDeclarationContext context,
        TypeShapeKnownSymbols knownSymbols,
        CancellationToken cancellationToken)
    {
        Parser parser = new(context.TypeSymbol, knownSymbols, cancellationToken);
        TypeId declaringTypeId = CreateTypeId(context.TypeSymbol);

        TypeDeclarationModel providerDeclaration = parser.CreateTypeDeclaration(context, declaringTypeId);
        if (providerDeclaration.IsValidTypeDeclaration)
        {
            // Only generate shapes if the context type is valid.
            parser.IncludeTypesFromGenerateShapeOfTAttributes(context.TypeSymbol);
        }

        return parser.ExportTypeShapeProviderModel(providerDeclaration, [], isGeneratedViaWitnessType: true);
    }

    public static TypeShapeProviderModel? ParseFromGenerateShapeAttributes(
        ImmutableArray<TypeWithAttributeDeclarationContext> generateShapeDeclarations,
        TypeShapeKnownSymbols knownSymbols,
        CancellationToken cancellationToken)
    {
        if (generateShapeDeclarations.IsEmpty)
        {
            return null;
        }

        Parser parser = new(knownSymbols.Compilation.Assembly, knownSymbols, cancellationToken);
        ImmutableEquatableArray<TypeDeclarationModel> generateShapeTypes = parser.IncludeTypesFromGenerateShapeAttributes(generateShapeDeclarations);
        return parser.ExportTypeShapeProviderModel(s_globalImplicitProviderDeclaration, generateShapeTypes, isGeneratedViaWitnessType: false);
    }

    private TypeShapeProviderModel ExportTypeShapeProviderModel(TypeDeclarationModel providerDeclaration, ImmutableEquatableArray<TypeDeclarationModel> generateShapeTypes, bool isGeneratedViaWitnessType)
    {
        Dictionary<TypeId, TypeShapeModel> generatedModels = new(GeneratedModels.Count);
        foreach (KeyValuePair<ITypeSymbol, TypeDataModel> entry in GeneratedModels)
        {
            TypeId typeId = CreateTypeId(entry.Value.Type);
            if (generatedModels.ContainsKey(typeId))
            {
                ReportDiagnostic(TypeNameConflict, location: null, typeId.FullyQualifiedName);
            }

            generatedModels[typeId] = MapModel(typeId, entry.Value, isGeneratedViaWitnessType);
        }

        return new TypeShapeProviderModel
        {
            Declaration = providerDeclaration,
            ProvidedTypes = generatedModels.ToImmutableEquatableDictionary(),
            GenerateShapeTypes = generateShapeTypes,
            Diagnostics = Diagnostics.ToImmutableEquatableSet(),
        };
    }

    private void IncludeTypesFromGenerateShapeOfTAttributes(ITypeSymbol declaringTypeSymbol)
    {
        Debug.Assert(declaringTypeSymbol.TypeKind is TypeKind.Class);

        foreach (AttributeData attributeData in declaringTypeSymbol.GetAttributes())
        {
            INamedTypeSymbol? attributeType = attributeData.AttributeClass;

            if (attributeType is { TypeArguments: [ITypeSymbol typeArgument] } &&
                SymbolEqualityComparer.Default.Equals(attributeType.ConstructedFrom, _knownSymbols.GenerateShapeAttributeOfT))
            {
                TypeDataModelGenerationStatus generationStatus = IncludeType(typeArgument);

                if (generationStatus is TypeDataModelGenerationStatus.UnsupportedType)
                {
                    ReportDiagnostic(TypeNotSupported, attributeData.GetLocation(), typeArgument.ToDisplayString());
                    continue;
                }

                if (generationStatus is TypeDataModelGenerationStatus.InaccessibleType)
                {
                    ReportDiagnostic(TypeNotAccessible, attributeData.GetLocation(), typeArgument.ToDisplayString());
                    continue;
                }
            }
        }
    }

    private ImmutableEquatableArray<TypeDeclarationModel> IncludeTypesFromGenerateShapeAttributes(ImmutableArray<TypeWithAttributeDeclarationContext> generateShapeDeclarations)
    {
        var typeDeclarations = new List<TypeDeclarationModel>();
        foreach (TypeWithAttributeDeclarationContext ctx in generateShapeDeclarations)
        {
            TypeDeclarationModel typeDeclaration = CreateTypeDeclaration(ctx, CreateTypeId(ctx.TypeSymbol));
            if (!typeDeclaration.IsValidTypeDeclaration)
            {
                continue; // Skip code generation if the declaring type is not valid.
            }

            TypeDataModelGenerationStatus generationStatus = IncludeType(ctx.TypeSymbol);

            if (generationStatus is TypeDataModelGenerationStatus.UnsupportedType)
            {
                ReportDiagnostic(TypeNotSupported, ctx.Declarations.First().Syntax.GetLocation(), ctx.TypeSymbol.ToDisplayString());
                continue;
            }

            if (generationStatus is TypeDataModelGenerationStatus.InaccessibleType)
            {
                ReportDiagnostic(TypeNotAccessible, ctx.Declarations.First().Syntax.GetLocation(), ctx.TypeSymbol.ToDisplayString());
                continue;
            }

            typeDeclarations.Add(typeDeclaration);
        }

        return typeDeclarations.ToImmutableEquatableArray();
    }

    private static TypeId CreateTypeId(ITypeSymbol type)
    {
        return new TypeId
        {
            FullyQualifiedName = type.GetFullyQualifiedName(),
            GeneratedPropertyName = type.GetGeneratedPropertyName(),
            IsValueType = type.IsValueType,
            SpecialType = type.OriginalDefinition.SpecialType,
        };
    }

    private TypeDeclarationModel CreateTypeDeclaration(TypeWithAttributeDeclarationContext context, TypeId typeId)
    {
        bool isValidTypeDeclaration = true;

        if (context.TypeSymbol.IsGenericTypeDefinition())
        {
            ReportDiagnostic(GenericTypeDefinitionsNotSupported, context.Declarations.First().Syntax.GetLocation(), context.TypeSymbol.ToDisplayString());
            isValidTypeDeclaration = false;
        }

        (BaseTypeDeclarationSyntax? declarationSyntax, SemanticModel? semanticModel) = context.Declarations.First();
        string typeDeclarationHeader = FormatTypeDeclarationHeader(declarationSyntax, context.TypeSymbol, out bool isPartialHierarchy);

        Stack<string>? parentStack = null;
        for (SyntaxNode? parentNode = declarationSyntax.Parent; parentNode is BaseTypeDeclarationSyntax parentType; parentNode = parentNode.Parent)
        {
            ITypeSymbol parentSymbol = semanticModel.GetDeclaredSymbol(parentType, CancellationToken)!;
            string parentHeader = FormatTypeDeclarationHeader(parentType, parentSymbol, out bool isPartialType);
            (parentStack ??= new()).Push(parentHeader);
            isPartialHierarchy &= isPartialType;
        }

        if (!isPartialHierarchy)
        {
            ReportDiagnostic(GeneratedTypeNotPartial, declarationSyntax.GetLocation(), context.TypeSymbol.ToDisplayString());
            isValidTypeDeclaration = false;
        }

        return new TypeDeclarationModel
        {
            Id = typeId,
            Name = context.TypeSymbol.Name,
            TypeDeclarationHeader = typeDeclarationHeader,
            ContainingTypes = parentStack?.ToImmutableEquatableArray() ?? [],
            Namespace = FormatNamespace(context.TypeSymbol),
            SourceFilenamePrefix = context.TypeSymbol.ToDisplayString(RoslynHelpers.QualifiedNameOnlyFormat),
            IsValidTypeDeclaration = isValidTypeDeclaration,
        };

        static string FormatTypeDeclarationHeader(BaseTypeDeclarationSyntax typeDeclaration, ITypeSymbol typeSymbol, out bool isPartialType)
        {
            StringBuilder stringBuilder = new();
            isPartialType = false;

            foreach (SyntaxToken modifier in typeDeclaration.Modifiers)
            {
                stringBuilder.Append(modifier.Text);
                stringBuilder.Append(' ');
                isPartialType |= modifier.IsKind(SyntaxKind.PartialKeyword);
            }

            stringBuilder.Append(typeDeclaration.GetTypeKindKeyword());
            stringBuilder.Append(' ');

            string typeName = typeSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
            stringBuilder.Append(typeName);

            return stringBuilder.ToString();
        }
    }

    private static string? FormatNamespace(ITypeSymbol type)
    {
        if (type.ContainingNamespace is { IsGlobalNamespace: false } ns)
        {
            return ns.ToDisplayString(RoslynHelpers.QualifiedNameOnlyFormat);
        }

        return null;
    }

    private static readonly TypeDeclarationModel s_globalImplicitProviderDeclaration = new()
    {
        Id = new()
        {
            FullyQualifiedName = "global::PolyType.SourceGenerator.GenerateShapeProvider",
            GeneratedPropertyName = "GenerateShapeProvider",
            IsValueType = false,
            SpecialType = SpecialType.None,
        },
        Name = "GenerateShapeProvider",
        Namespace = "PolyType.SourceGenerator",
        SourceFilenamePrefix = "GenerateShapeProvider",
        TypeDeclarationHeader = "internal partial class GenerateShapeProvider",
        IsValidTypeDeclaration = true,
        ContainingTypes = [],
    };
}
