using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Diagnostics;
using TypeShape.SourceGenerator.Helpers;
using TypeShape.SourceGenerator.Model;

namespace TypeShape.SourceGenerator;

public sealed partial class ModelGenerator
{
    private readonly KnownSymbols _knownSymbols;
    private readonly SemanticModel _semanticModel;
    private readonly CancellationToken _cancellationToken;
    private readonly ClassDeclarationSyntax _classDeclarationSyntax;
    private readonly ITypeSymbol _declaredTypeSymbol;

    private readonly Dictionary<ITypeSymbol, TypeModel> _generatedTypes = new(SymbolEqualityComparer.Default);
    private readonly Queue<(TypeId, ITypeSymbol)> _typesToGenerate = new();
    private readonly List<DiagnosticInfo> _diagnostics = new();

    public ModelGenerator(KnownSymbols knownSymbols, ClassDeclarationSyntax classDeclarationSyntax, SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        _classDeclarationSyntax = classDeclarationSyntax;
        _cancellationToken = cancellationToken;
        _semanticModel = semanticModel;
        _knownSymbols = knownSymbols;
        _declaredTypeSymbol = semanticModel.GetDeclaredSymbol(classDeclarationSyntax, cancellationToken)!;
    }

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
            Namespace = FormatNamespace(_declaredTypeSymbol),
            ProvidedTypes = _generatedTypes.Values.OrderBy(type => type.Id.FullyQualifiedName).ToImmutableEquatableArray(),
            TypeDeclaration = ResolveDeclarationHeader(_classDeclarationSyntax, out ImmutableEquatableArray<string>? containingTypes),
            ContainingTypes = containingTypes,
            Diagnostics = _diagnostics.ToImmutableEquatableArray(),
        };
    }

    private void TraverseTypeGraph()
    {
        while (_typesToGenerate.Count > 0)
        {
            _cancellationToken.ThrowIfCancellationRequested();

            (TypeId typeId, ITypeSymbol type) = _typesToGenerate.Dequeue();
            if (!_generatedTypes.ContainsKey(type))
            {
                TypeModel generatedType = MapType(typeId, type);
                _generatedTypes.Add(type, generatedType);
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

        ITypeSymbol[]? classTupleElements = _semanticModel.Compilation.GetClassTupleElements(_knownSymbols.CoreLibAssembly, type);
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

            if (SymbolEqualityComparer.Default.Equals(attributeType, _knownSymbols.GenerateShapeAttributeType))
            {
                Debug.Assert(attributeData.ConstructorArguments.Length == 1);
                ITypeSymbol? typeSymbol = attributeData.ConstructorArguments[0].Value as ITypeSymbol;

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
        type = _semanticModel.Compilation.EraseCompilerMetadata(type);

        if (_generatedTypes.TryGetValue(type, out TypeModel? generated))
        {
            return generated.Id;
        }

        TypeId typeId = CreateTypeId(type);
        _typesToGenerate.Enqueue((typeId, type));
        return typeId;
    }

    private static TypeId CreateTypeId(ITypeSymbol type)
    {
        return new TypeId
        {
            FullyQualifiedName = type.GetFullyQualifiedName(),
            GeneratedPropertyName = type.GetGeneratedPropertyName(),
        };
    }

    private string ResolveDeclarationHeader(ClassDeclarationSyntax classSyntax, out ImmutableEquatableArray<string> parentHeaders)
    {
        bool hierarchyNotPartial = !IsSyntaxKind(classSyntax, SyntaxKind.PartialKeyword);

        Stack<string>? parents = null;
        for (SyntaxNode? current = classSyntax.Parent; current is TypeDeclarationSyntax parent; current = current.Parent)
        {
            hierarchyNotPartial |= !IsSyntaxKind(parent, SyntaxKind.PartialKeyword);
            (parents ??= new()).Push(FormatTypeDeclarationHeader(parent));
        }

        if (hierarchyNotPartial)
        {
            ReportDiagnostic(ProviderTypeNotPartial, classSyntax.GetLocation(), classSyntax.Identifier);
        }

        parentHeaders = parents != null ? parentHeaders = parents.ToImmutableEquatableArray() : ImmutableEquatableArray.Empty<string>();
        return FormatTypeDeclarationHeader(classSyntax);

        static string FormatTypeDeclarationHeader(TypeDeclarationSyntax classSyntax)
        {
            string accessibilityModifier =
                IsSyntaxKind(classSyntax, SyntaxKind.PublicKeyword) ? "public " :
                IsSyntaxKind(classSyntax, SyntaxKind.InternalKeyword) ? "internal " :
                IsSyntaxKind(classSyntax, SyntaxKind.PrivateKeyword) ? "private " : "";

            string kindToken = classSyntax.Kind() == SyntaxKind.ClassDeclaration ? "class" : "struct";

            return $"{accessibilityModifier}partial {kindToken} {classSyntax.Identifier.ValueText}";
        }

        static bool IsSyntaxKind(TypeDeclarationSyntax classSyntax, SyntaxKind kind)
            => classSyntax.Modifiers.Any(m => m.IsKind(kind));
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
        => _semanticModel.Compilation.IsSymbolAccessibleWithin(symbol, _declaredTypeSymbol);

    private static bool IsSupportedType(ITypeSymbol type)
        => type.TypeKind is not (TypeKind.Pointer or TypeKind.Error) &&
          !type.IsRefLikeType && type.SpecialType is not SpecialType.System_Void &&
          !type.ContainsGenericParameters();

    private bool DisallowMemberResolution(ITypeSymbol type)
    {
        return _semanticModel.Compilation.IsAtomicValueType(_knownSymbols.CoreLibAssembly, type) ||
            type.TypeKind is TypeKind.Array or TypeKind.Enum ||
            type.SpecialType is SpecialType.System_Nullable_T ||
            _knownSymbols.MemberInfoType.IsAssignableFrom(type) ||
            _knownSymbols.DelegateType.IsAssignableFrom(type);
    }
}
