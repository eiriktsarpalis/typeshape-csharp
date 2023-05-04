using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TypeShape.SourceGenerator.Helpers;
using TypeShape.SourceGenerator.Model;

namespace TypeShape.SourceGenerator;

public sealed partial class ModelGenerator
{
    private const string GlobalNamespacePrefix = "global::";
    private const string GenerateShapeAttributeFQN = "global::TypeShape.GenerateShapeAttribute";
    private const string GlobalNamespaceIdentifier = "<global namespace>";

    private readonly SemanticModel _semanticModel;
    private readonly CancellationToken _cancellationToken;
    private readonly ClassDeclarationSyntax _classDeclarationSyntax;
    private readonly ITypeSymbol _declaredTypeSymbol;

    private readonly Dictionary<ITypeSymbol, TypeModel> _generatedTypes = new(SymbolEqualityComparer.Default);
    private readonly Queue<(TypeId, ITypeSymbol)> _typesToGenerate = new();
    private readonly List<Diagnostic> _diagnostics = new();

    private readonly ITypeSymbol _delegateType;
    private readonly ITypeSymbol? _memberInfoType;
    private readonly ITypeSymbol? _iReadOnlyDictionaryOfTKeyTValue;
    private readonly ITypeSymbol? _iDictionaryOfTKeyTValue;
    private readonly ITypeSymbol? _iDictionary;
    private readonly ITypeSymbol? _iList;

    private readonly ITypeSymbol? _immutableArray;
    private readonly ITypeSymbol? _immutableList;
    private readonly ITypeSymbol? _immutableQueue;
    private readonly ITypeSymbol? _immutableStack;
    private readonly ITypeSymbol? _immutableHashSet;
    private readonly ITypeSymbol? _immutableSortedSet;
    private readonly ITypeSymbol? _immutableDictionary;
    private readonly ITypeSymbol? _immutableSortedDictionary;

    public ModelGenerator(ClassDeclarationSyntax classDeclarationSyntax, Compilation compilation, CancellationToken cancellationToken)
    {
        _classDeclarationSyntax = classDeclarationSyntax;
        _cancellationToken = cancellationToken;
        _semanticModel = compilation.GetSemanticModel(classDeclarationSyntax.SyntaxTree);

        _declaredTypeSymbol = _semanticModel.GetDeclaredSymbol(classDeclarationSyntax, cancellationToken)!;
        _iReadOnlyDictionaryOfTKeyTValue = compilation.GetTypeByMetadataName("System.Collections.Generic.IReadOnlyDictionary`2");
        _iDictionaryOfTKeyTValue = compilation.GetTypeByMetadataName("System.Collections.Generic.IDictionary`2");
        _iDictionary = compilation.GetTypeByMetadataName("System.Collections.IDictionary");
        _iList = compilation.GetTypeByMetadataName("System.Collections.IList");
        _delegateType = compilation.GetSpecialType(SpecialType.System_Delegate);
        _memberInfoType = compilation.GetTypeByMetadataName("System.Reflection.MemberInfo");

        _immutableArray = compilation.GetTypeByMetadataName("System.Collections.Immutable.ImmutableArray`1");
        _immutableList = compilation.GetTypeByMetadataName("System.Collections.Immutable.ImmutableList`1");
        _immutableQueue = compilation.GetTypeByMetadataName("System.Collections.Immutable.ImmutableQueue`1");
        _immutableStack = compilation.GetTypeByMetadataName("System.Collections.Immutable.ImmutableStack`1");
        _immutableHashSet = compilation.GetTypeByMetadataName("System.Collections.Immutable.ImmutableHashSet`1");
        _immutableSortedSet = compilation.GetTypeByMetadataName("System.Collections.Immutable.ImmutableSortedSet`1");
        _immutableDictionary = compilation.GetTypeByMetadataName("System.Collections.Immutable.ImmutableDictionary`2");
        _immutableSortedDictionary = compilation.GetTypeByMetadataName("System.Collections.Immutable.ImmutableSortedDictionary`2");
    }

    public static TypeShapeProviderModel Compile(ClassDeclarationSyntax classDeclarationSyntax, Compilation compilation, CancellationToken cancellationToken)
    {
        ModelGenerator compiler = new(classDeclarationSyntax, compilation, cancellationToken);
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
            ProvidedTypes = _generatedTypes.Values.OrderBy(type => type.Id.FullyQualifiedName).ToImmutableArrayEq(),
            TypeDeclaration = ResolveDeclarationHeader(_classDeclarationSyntax, out ImmutableArrayEq<string>? containingTypes),
            ContainingTypes = containingTypes,
            Diagnostics = _diagnostics.ToImmutableArrayEq(),
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

        ITypeSymbol[]? classTupleElements = _semanticModel.Compilation.GetClassTupleElements(type);
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
        foreach (AttributeSyntax attributeSyntax in _classDeclarationSyntax.AttributeLists.SelectMany(attrList => attrList.Attributes))
        {
            if (_semanticModel.GetSymbolInfo(attributeSyntax, _cancellationToken).Symbol is not IMethodSymbol attributeSymbol)
            {
                continue;
            }

            INamedTypeSymbol attributeTypeSymbol = attributeSymbol.ContainingType;
            if (attributeTypeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == GenerateShapeAttributeFQN &&
                ReadTypeSymbolFromGenerateShapeAttribute(attributeSyntax) is { } typeSymbol)
            {
                if (!IsSupportedType(typeSymbol) || !IsAccessibleFromGeneratedType(typeSymbol))
                {
                    ReportDiagnostic(
                        Diagnostic.Create(TypeNotSupported, attributeSyntax.GetLocationTrimmed(), typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));

                    continue;
                }

                EnqueueForGeneration(typeSymbol);
            }
        }
    }

    private ITypeSymbol? ReadTypeSymbolFromGenerateShapeAttribute(AttributeSyntax attributeSyntax)
    {
        foreach (SyntaxNode syntaxNode in attributeSyntax.DescendantNodes())
        {
            if (syntaxNode is AttributeArgumentSyntax &&
                syntaxNode.ChildNodes().FirstOrDefault() is TypeOfExpressionSyntax typeofExpr)
            {
                SyntaxNode typeExpr = typeofExpr.ChildNodes().Single();
                return _semanticModel.GetTypeInfo(typeExpr, _cancellationToken).ConvertedType;
            }
        }

        return null;
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

    private string ResolveDeclarationHeader(ClassDeclarationSyntax classSyntax, out ImmutableArrayEq<string> parentHeaders)
    {
        bool hierarchyNotPartial = !IsSyntaxKind(classSyntax, SyntaxKind.PartialKeyword);

        Stack<string>? parents = null;
        for (SyntaxNode? current = classSyntax.Parent; current is ClassDeclarationSyntax parent; current = current.Parent)
        {
            hierarchyNotPartial |= !IsSyntaxKind(parent, SyntaxKind.PartialKeyword);
            (parents ??= new()).Push(FormatTypeDeclarationHeader(parent));
        }

        if (hierarchyNotPartial)
        {
            ReportDiagnostic(Diagnostic.Create(ProviderTypeNotPartial, classSyntax.GetLocationTrimmed(), classSyntax.Identifier));
        }

        parentHeaders = parents != null ? parentHeaders = parents.ToImmutableArrayEq() : ImmutableArrayEq<string>.Empty;
        return FormatTypeDeclarationHeader(classSyntax);

        static string FormatTypeDeclarationHeader(ClassDeclarationSyntax classSyntax)
        {
            string accessibilityModifier =
                IsSyntaxKind(classSyntax, SyntaxKind.PublicKeyword) ? "public " :
                IsSyntaxKind(classSyntax, SyntaxKind.InternalKeyword) ? "internal " :
                IsSyntaxKind(classSyntax, SyntaxKind.PrivateKeyword) ? "private " : "";

            string kindToken = classSyntax.Kind() == SyntaxKind.ClassDeclaration ? "class" : "struct";

            return $"{accessibilityModifier}partial {kindToken} {classSyntax.Identifier.ValueText}";
        }

        static bool IsSyntaxKind(ClassDeclarationSyntax classSyntax, SyntaxKind kind)
            => classSyntax.Modifiers.Any(m => m.IsKind(kind));
    }

    private static string? FormatNamespace(ITypeSymbol type)
    {
        if (type.ContainingNamespace is { } ns)
        {
            string fmt = ns.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            return fmt switch
            {
                GlobalNamespaceIdentifier => null,
                _ when (fmt.StartsWith(GlobalNamespacePrefix, StringComparison.Ordinal)) => fmt.Remove(0, GlobalNamespacePrefix.Length),
                _ => fmt
            };
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
        return _semanticModel.Compilation.IsAtomicValueType(type) ||
            type.TypeKind is TypeKind.Array or TypeKind.Enum ||
            type.SpecialType is SpecialType.System_Nullable_T ||
            _memberInfoType.IsAssignableFrom(type) ||
            _delegateType.IsAssignableFrom(type);
    }
}
