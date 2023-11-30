using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;
using TypeShape.SourceGenerator.Helpers;
using TypeShape.SourceGenerator.Model;

namespace TypeShape.SourceGenerator;

using TypeToGenerate = (TypeId Id, ITypeSymbol Symbol, bool EmitGenericTypeShapeProviderImplementation);

public sealed partial class ModelGenerator(
    ISymbol generationScope, 
    KnownSymbols knownSymbols, 
    CancellationToken cancellationToken)
{
    private readonly Dictionary<ITypeSymbol, TypeId> _visitedTypes = new(SymbolEqualityComparer.Default);
    private readonly Dictionary<TypeId, TypeModel> _generatedModels = new();
    private readonly Queue<TypeToGenerate> _typesToGenerate = new();
    private readonly List<DiagnosticInfo> _diagnostics = [];

    public static TypeShapeProviderModel CompileFromGenerateShapeAttributes(
        TypeWithAttributeDeclarationContext context,
        KnownSymbols knownSymbols,
        CancellationToken cancellationToken)
    {
        ModelGenerator compiler = new(context.TypeSymbol, knownSymbols, cancellationToken);
        TypeId declaringTypeId = CreateTypeId(context.TypeSymbol);

        TypeDeclarationModel providerDeclaration = compiler.CreateTypeDeclaration(context, declaringTypeId);
        compiler.EnqueueTypesFromGenerateShapeOfTAttributes(context.TypeSymbol);

        return compiler.Compile(providerDeclaration, []);
    }

    public static TypeShapeProviderModel? CompileFromGenerateShapeAttributes(
        ImmutableArray<TypeWithAttributeDeclarationContext> generateShapeDeclarations,
        KnownSymbols knownSymbols,
        CancellationToken cancellationToken)
    {
        if (generateShapeDeclarations.IsEmpty)
        {
            return null;
        }

        ModelGenerator compiler = new(knownSymbols.Compilation.Assembly, knownSymbols, cancellationToken);
        ImmutableEquatableArray<TypeDeclarationModel> generateShapeTypes = compiler.EnqueueTypesFromGenerateShapeAttributes(generateShapeDeclarations);
        TypeDeclarationModel providerDeclaration = new()
        {
            Id = new()
            {
                FullyQualifiedName = "global::TypeShape.SourceGenerator.GenerateShapeProvider",
                GeneratedPropertyName = "GenerateShapeProvider",
                IsValueType = false,
                SpecialType = SpecialType.None,
            },
            Name = "GenerateShapeProvider",
            Namespace = "TypeShape.SourceGenerator",
            SourceFilenamePrefix = "GenerateShapeProvider",
            TypeDeclarationHeader = "internal partial class GenerateShapeProvider",
            ContainingTypes = [],
        };

        return compiler.Compile(providerDeclaration, generateShapeTypes);
    }

    public TypeShapeProviderModel Compile(TypeDeclarationModel providerDeclaration, ImmutableEquatableArray<TypeDeclarationModel> generateShapeTypes)
    {
        TraverseTypeGraph();

        return new TypeShapeProviderModel
        {
            Declaration = providerDeclaration,
            ProvidedTypes = _generatedModels.ToImmutableEquatableDictionary(),
            GenerateShapeTypes = generateShapeTypes,
            Diagnostics = _diagnostics.ToImmutableEquatableSet(),
        };
    }

    private void TraverseTypeGraph()
    {
        while (_typesToGenerate.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            TypeToGenerate typeToGenerate = _typesToGenerate.Dequeue();
            if (_generatedModels.ContainsKey(typeToGenerate.Id))
            {
                ReportDiagnostic(TypeNameConflict, typeToGenerate.Symbol.Locations.FirstOrDefault(), typeToGenerate.Id.FullyQualifiedName);
            }
            else
            {
                TypeModel generatedType = MapType(typeToGenerate.Id, typeToGenerate.Symbol, typeToGenerate.EmitGenericTypeShapeProviderImplementation);
                _generatedModels.Add(typeToGenerate.Id, generatedType);
            }
        }
    }

    private TypeModel MapType(TypeId typeId, ITypeSymbol type, bool emitGenericTypeShapeProviderImplementation)
    {
        bool isSpecialTypeKind = TryResolveSpecialTypeKinds(typeId, type,
            out EnumTypeModel? enumType,
            out NullableTypeModel? nullableType, 
            out DictionaryTypeModel? dictionaryType, 
            out EnumerableTypeModel? enumerableType);

        bool disallowMemberResolution = isSpecialTypeKind || DisallowMemberResolution(type);
        ISymbol[] propertiesOrFields = ResolvePropertyAndFieldSymbols(type, disallowMemberResolution).ToArray();
        ITypeSymbol[]? classTupleElements = knownSymbols.Compilation.GetClassTupleElements(knownSymbols.CoreLibAssembly, type);

        return new TypeModel
        {
            Id = typeId,
            Properties = MapProperties(typeId, type, propertiesOrFields, classTupleElements, disallowMemberResolution),
            Constructors = MapConstructors(typeId, type, propertiesOrFields, classTupleElements, disallowMemberResolution),
            EnumType = enumType,
            NullableType = nullableType,
            EnumerableType = enumerableType,
            DictionaryType = dictionaryType,
            IsValueTupleType = type.IsNonTrivialValueTupleType(),
            IsClassTupleType = classTupleElements is not null,
            EmitGenericTypeShapeProviderImplementation = emitGenericTypeShapeProviderImplementation,
        };
    }

    private void EnqueueTypesFromGenerateShapeOfTAttributes(ITypeSymbol declaringTypeSymbol)
    {
        Debug.Assert(declaringTypeSymbol.TypeKind is TypeKind.Class);

        foreach (AttributeData attributeData in declaringTypeSymbol.GetAttributes())
        {
            INamedTypeSymbol? attributeType = attributeData.AttributeClass;

            if (attributeType is { TypeArguments: [ITypeSymbol typeArgument] } &&
                SymbolEqualityComparer.Default.Equals(attributeType.ConstructedFrom, knownSymbols.GenerateShapeAttributeOfT))
            {
                if (!IsSupportedType(typeArgument))
                {
                    ReportDiagnostic(TypeNotSupported, attributeData.GetLocation(), typeArgument.ToDisplayString());
                    continue;
                }

                Debug.Assert(IsAccessibleFromGeneratedType(typeArgument));
                EnqueueForGeneration(typeArgument, emitGenericTypeShapeProviderImplementation: true);
            }
        }
    }

    private ImmutableEquatableArray<TypeDeclarationModel> EnqueueTypesFromGenerateShapeAttributes(ImmutableArray<TypeWithAttributeDeclarationContext> generateShapeDeclarations)
    {
        List<TypeDeclarationModel> generateTypeDeclarations = [];

        foreach (TypeWithAttributeDeclarationContext ctx in generateShapeDeclarations)
        {
            if (ctx.TypeSymbol.IsGenericTypeDefinition())
            {
                ReportDiagnostic(GenericTypeDefinitionsNotSupported, ctx.DeclarationSyntax.GetLocation(), ctx.TypeSymbol.ToDisplayString());
                continue;
            }

            if (!IsAccessibleFromGeneratedType(ctx.TypeSymbol))
            {
                ReportDiagnostic(TypeNotAccessible, ctx.DeclarationSyntax.GetLocation(), ctx.TypeSymbol.ToDisplayString());
                continue;
            }

            Debug.Assert(IsSupportedType(ctx.TypeSymbol));
            TypeId typeId = EnqueueForGeneration(ctx.TypeSymbol);
            TypeDeclarationModel typeDeclaration = CreateTypeDeclaration(ctx, typeId);
            generateTypeDeclarations.Add(typeDeclaration);
        }

        return [..generateTypeDeclarations];
    }

    private TypeId EnqueueForGeneration(ITypeSymbol type, bool emitGenericTypeShapeProviderImplementation = false)
    {
        type = knownSymbols.Compilation.EraseCompilerMetadata(type);

        if (_visitedTypes.TryGetValue(type, out TypeId id))
        {
            return id;
        }

        TypeId typeId = CreateTypeId(type);
        _typesToGenerate.Enqueue((typeId, type, emitGenericTypeShapeProviderImplementation));
        _visitedTypes.Add(type, typeId);
        return typeId;
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
        string typeDeclarationHeader = FormatTypeDeclarationHeader(context.DeclarationSyntax, context.TypeSymbol, cancellationToken, out bool isPartialHierarchy);

        Stack<string>? parentStack = null;
        for (SyntaxNode? parentNode = context.DeclarationSyntax.Parent; parentNode is BaseTypeDeclarationSyntax parentType; parentNode = parentNode.Parent)
        {
            ITypeSymbol parentSymbol = context.SemanticModel.GetDeclaredSymbol(parentType, cancellationToken)!;
            string parentHeader = FormatTypeDeclarationHeader(parentType, parentSymbol, cancellationToken, out bool isPartialType);
            (parentStack ??= new()).Push(parentHeader);
            isPartialHierarchy &= isPartialType;
        }

        if (!isPartialHierarchy)
        {
            ReportDiagnostic(GeneratedTypeNotPartial, context.DeclarationSyntax.GetLocation(), context.TypeSymbol.ToDisplayString());
        }

        return new TypeDeclarationModel
        {
            Id = typeId,
            Name = context.TypeSymbol.Name,
            TypeDeclarationHeader = typeDeclarationHeader,
            ContainingTypes = parentStack?.ToImmutableEquatableArray() ?? [],
            Namespace = FormatNamespace(context.TypeSymbol),
            SourceFilenamePrefix = context.TypeSymbol.ToDisplayString(RoslynHelpers.QualifiedNameOnlyFormat),
        };

        static string FormatTypeDeclarationHeader(BaseTypeDeclarationSyntax typeDeclaration, ITypeSymbol typeSymbol, CancellationToken cancellationToken, out bool isPartialType)
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

            Debug.Assert(typeSymbol != null);

            string typeName = typeSymbol!.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
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

    private bool IsAccessibleFromGeneratedType(ISymbol symbol)
        => knownSymbols.Compilation.IsSymbolAccessibleWithin(symbol, within: generationScope);

    private static bool IsSupportedType(ITypeSymbol type)
        => type.TypeKind is not (TypeKind.Pointer or TypeKind.Error or TypeKind.Delegate) &&
          !type.IsRefLikeType && type.SpecialType is not SpecialType.System_Void &&
          !type.ContainsGenericParameters();

    private bool DisallowMemberResolution(ITypeSymbol type)
    {
        return knownSymbols.Compilation.IsAtomicValueType(knownSymbols.CoreLibAssembly, type) ||
            type.TypeKind is TypeKind.Array or TypeKind.Enum ||
            type.SpecialType is SpecialType.System_Nullable_T ||
            knownSymbols.MemberInfoType.IsAssignableFrom(type) ||
            knownSymbols.DelegateType.IsAssignableFrom(type);
    }
}
