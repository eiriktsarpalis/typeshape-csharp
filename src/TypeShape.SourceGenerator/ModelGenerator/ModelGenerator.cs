using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Diagnostics;
using System.Text;
using TypeShape.SourceGenerator.Helpers;
using TypeShape.SourceGenerator.Model;

namespace TypeShape.SourceGenerator;

public sealed partial class ModelGenerator(
    KnownSymbols knownSymbols, 
    ClassDeclarationSyntax classDeclarationSyntax, 
    SemanticModel semanticModel, 
    CancellationToken cancellationToken)
{
    private readonly ITypeSymbol _declaredTypeSymbol = semanticModel.GetDeclaredSymbol(classDeclarationSyntax, cancellationToken)!;
    private readonly Dictionary<ITypeSymbol, TypeId> _visitedTypes = new(SymbolEqualityComparer.Default);
    private readonly Dictionary<TypeId, TypeModel> _generatedModels = new();
    private readonly Queue<(TypeId, ITypeSymbol)> _typesToGenerate = new();
    private readonly List<DiagnosticInfo> _diagnostics = [];

    public static TypeShapeProviderModel Compile(KnownSymbols knownSymbols, ClassDeclarationSyntax classDeclarationSyntax, SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        ModelGenerator compiler = new(knownSymbols, classDeclarationSyntax, semanticModel, cancellationToken);
        return compiler.Compile();
    }

    public TypeShapeProviderModel Compile()
    {
        ReadConfigurationFromAttributes();
        TraverseTypeGraph();

        return new TypeShapeProviderModel
        {
            Name = _declaredTypeSymbol.Name,
            SourceFilenamePrefix = _declaredTypeSymbol.ToDisplayString(RoslynHelpers.QualifiedNameOnlyFormat),
            Namespace = FormatNamespace(_declaredTypeSymbol),
            ProvidedTypes = _generatedModels.ToImmutableEquatableDictionary(),
            TypeDeclaration = ResolveDeclarationHeader(classDeclarationSyntax, out ImmutableEquatableArray<string>? containingTypes),
            ContainingTypes = containingTypes,
            Diagnostics = _diagnostics.ToImmutableEquatableSet(),
        };
    }

    private void TraverseTypeGraph()
    {
        while (_typesToGenerate.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            (TypeId typeId, ITypeSymbol type) = _typesToGenerate.Dequeue();
            if (_generatedModels.ContainsKey(typeId))
            {
                ReportDiagnostic(TypeNameConflict, type.Locations.FirstOrDefault(), typeId.FullyQualifiedName);
            }
            else
            {
                TypeModel generatedType = MapType(typeId, type);
                _generatedModels.Add(typeId, generatedType);
            }
        }
    }

    private TypeModel MapType(TypeId typeId, ITypeSymbol type)
    {
        bool isSpecialTypeKind = TryResolveSpecialTypeKinds(typeId, type,
            out EnumTypeModel? enumType,
            out NullableTypeModel? nullableType, 
            out DictionaryTypeModel? dictionaryType, 
            out EnumerableTypeModel? enumerableType, 
            out ITypeSymbol? implementedCollectionType);

        ITypeSymbol[]? classTupleElements = semanticModel.Compilation.GetClassTupleElements(knownSymbols.CoreLibAssembly, type);
        bool disallowMemberResolution = DisallowMemberResolution(type);

        return new TypeModel
        {
            Id = typeId,
            Properties = MapProperties(typeId, type, classTupleElements, disallowMemberResolution: disallowMemberResolution || isSpecialTypeKind),
            Constructors = MapConstructors(typeId, type, classTupleElements, implementedCollectionType, disallowMemberResolution),
            EnumType = enumType,
            NullableType = nullableType,
            EnumerableType = enumerableType,
            DictionaryType = dictionaryType,
            IsValueTupleType = type.IsNonTrivialValueTupleType(),
            IsClassTupleType = classTupleElements is not null,
        };
    }

    private void ReadConfigurationFromAttributes()
    {
        foreach (AttributeData attributeData in _declaredTypeSymbol.GetAttributes())
        {
            INamedTypeSymbol? attributeType = attributeData.AttributeClass;

            if (SymbolEqualityComparer.Default.Equals(attributeType, knownSymbols.GenerateShapeAttributeType) &&
                attributeData.ConstructorArguments is [ { Value: var value } ] &&
                value is null or ITypeSymbol)
            {
                ITypeSymbol? typeSymbol = value as ITypeSymbol;

                if (typeSymbol is null || !IsSupportedType(typeSymbol) || !IsAccessibleFromGeneratedType(typeSymbol))
                {
                    ReportDiagnostic(TypeNotSupported, attributeData.GetLocation(), typeSymbol?.ToDisplayString() ?? "null");
                    continue;
                }

                EnqueueForGeneration(typeSymbol);
            }
        }
    }

    private TypeId EnqueueForGeneration(ITypeSymbol type)
    {
        type = semanticModel.Compilation.EraseCompilerMetadata(type);

        if (_visitedTypes.TryGetValue(type, out TypeId id))
        {
            return id;
        }

        TypeId typeId = CreateTypeId(type);
        _typesToGenerate.Enqueue((typeId, type));
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

    private string ResolveDeclarationHeader(ClassDeclarationSyntax classSyntax, out ImmutableEquatableArray<string> parentHeaders)
    {
        string typeDeclarationHeader = FormatTypeDeclarationHeader(classSyntax, semanticModel, cancellationToken, out bool isPartialHierarchy);

        Stack<string>? parentStack = null;
        for (SyntaxNode? parentNode = classSyntax.Parent; parentNode is TypeDeclarationSyntax parentType; parentNode = parentNode.Parent)
        {
            string parentHeader = FormatTypeDeclarationHeader(parentType, semanticModel, cancellationToken, out bool isPartialType);
            (parentStack ??= new()).Push(parentHeader);
            isPartialHierarchy &= isPartialType;
        }

        if (!isPartialHierarchy)
        {
            ReportDiagnostic(ProviderTypeNotPartial, classSyntax.GetLocation(), classSyntax.Identifier);
        }

        parentHeaders = parentStack?.ToImmutableEquatableArray() ?? [];
        return typeDeclarationHeader;

        static string FormatTypeDeclarationHeader(TypeDeclarationSyntax typeDeclaration, SemanticModel semanticModel, CancellationToken cancellationToken, out bool isPartialType)
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

            INamedTypeSymbol? typeSymbol = semanticModel.GetDeclaredSymbol(typeDeclaration, cancellationToken);
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
        => semanticModel.Compilation.IsSymbolAccessibleWithin(symbol, _declaredTypeSymbol);

    private static bool IsSupportedType(ITypeSymbol type)
        => type.TypeKind is not (TypeKind.Pointer or TypeKind.Error) &&
          !type.IsRefLikeType && type.SpecialType is not SpecialType.System_Void &&
          !type.ContainsGenericParameters();

    private bool DisallowMemberResolution(ITypeSymbol type)
    {
        return semanticModel.Compilation.IsAtomicValueType(knownSymbols.CoreLibAssembly, type) ||
            type.TypeKind is TypeKind.Array or TypeKind.Enum ||
            type.SpecialType is SpecialType.System_Nullable_T ||
            knownSymbols.MemberInfoType.IsAssignableFrom(type) ||
            knownSymbols.DelegateType.IsAssignableFrom(type);
    }
}
