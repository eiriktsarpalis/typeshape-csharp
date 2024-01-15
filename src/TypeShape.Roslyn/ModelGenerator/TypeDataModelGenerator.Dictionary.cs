using Microsoft.CodeAnalysis;
using TypeShape.Roslyn.Helpers;

namespace TypeShape.Roslyn;

public partial class TypeDataModelGenerator
{
    private bool TryMapDictionary(ITypeSymbol type, ref TypeDataModelGenerationContext ctx, out TypeDataModel? model, out TypeDataModelGenerationStatus status)
    {
        model = null;
        status = default;

        if (type is not INamedTypeSymbol namedType)
        {
            // Only named types can be dictionaries
            return false;
        }

        DictionaryKind kind = default;
        CollectionConstructionStrategy constructionStrategy = CollectionConstructionStrategy.None;
        INamedTypeSymbol? implementationType = null;
        IMethodSymbol? spanFactory = null;
        IMethodSymbol? enumerableFactory = null;
        ITypeSymbol? keyType = null;
        ITypeSymbol? valueType = null;

        if (type.GetCompatibleGenericBaseType(KnownSymbols.IReadOnlyDictionaryOfTKeyTValue) is { } genericReadOnlyIDictInstance)
        {
            keyType = genericReadOnlyIDictInstance.TypeArguments[0];
            valueType = genericReadOnlyIDictInstance.TypeArguments[1];
            kind = DictionaryKind.IReadOnlyDictionaryOfKV;
        }
        else if (type.GetCompatibleGenericBaseType(KnownSymbols.IDictionaryOfTKeyTValue) is { } genericIDictInstance)
        {
            keyType = genericIDictInstance.TypeArguments[0];
            valueType = genericIDictInstance.TypeArguments[1];
            kind = DictionaryKind.IDictionaryOfKV;
        }
        else if (KnownSymbols.IDictionary.IsAssignableFrom(type))
        {
            keyType = KnownSymbols.Compilation.ObjectType;
            valueType = KnownSymbols.Compilation.ObjectType;
            kind = DictionaryKind.IDictionary;
        }
        else
        {
            return false; // Not a dictionary type
        }

        if (namedType.Constructors.Any(ctor => ctor.Parameters.Length == 0 && !ctor.IsStatic && IsAccessibleSymbol(ctor)) &&
            ContainsSettableIndexer(type, keyType, valueType))
        {
            constructionStrategy = CollectionConstructionStrategy.Mutable;
        }
        else if (namedType.Constructors.Any(ctor =>
            IsAccessibleSymbol(ctor) &&
            ctor.Parameters is [{ Type: INamedTypeSymbol parameterType }] &&
            SymbolEqualityComparer.Default.Equals(parameterType.ConstructedFrom, KnownSymbols.ReadOnlySpanOfT) &&
            parameterType.TypeArguments is [INamedTypeSymbol elementType] &&
            SymbolEqualityComparer.Default.Equals(elementType.ConstructedFrom, KnownSymbols.KeyValuePairOfKV) &&
            elementType.TypeArguments is [INamedTypeSymbol k, INamedTypeSymbol v] &&
            SymbolEqualityComparer.Default.Equals(k, keyType) &&
            SymbolEqualityComparer.Default.Equals(v, valueType)))
        {
            constructionStrategy = CollectionConstructionStrategy.Span;
        }
        else if (namedType.Constructors.Any(ctor =>
            IsAccessibleSymbol(ctor) &&
            ctor.Parameters is [{ Type: INamedTypeSymbol parameterType }] &&
            SymbolEqualityComparer.Default.Equals(parameterType.ConstructedFrom, KnownSymbols.IEnumerableOfT) &&
            parameterType.TypeArguments is [INamedTypeSymbol elementType] &&
            SymbolEqualityComparer.Default.Equals(elementType.ConstructedFrom, KnownSymbols.KeyValuePairOfKV) &&
            elementType.TypeArguments is [INamedTypeSymbol k, INamedTypeSymbol v] &&
            SymbolEqualityComparer.Default.Equals(k, keyType) &&
            SymbolEqualityComparer.Default.Equals(v, valueType)))
        {
            constructionStrategy = CollectionConstructionStrategy.Enumerable;
        }

        if (namedType.TypeKind is TypeKind.Interface)
        {
            if (namedType.TypeArguments.Length == 2 && KnownSymbols.DictionaryOfTKeyTValue?.GetCompatibleGenericBaseType(namedType.ConstructedFrom) != null)
            {
                // Handle IDictionary<TKey, TValue> and IReadOnlyDictionary<TKey, TValue> using Dictionary<TKey, TValue>
                INamedTypeSymbol dictOfTKeyTValue = KnownSymbols.DictionaryOfTKeyTValue!.Construct(keyType, valueType);
                constructionStrategy = CollectionConstructionStrategy.Mutable;
                implementationType = dictOfTKeyTValue;
            }
            else if (SymbolEqualityComparer.Default.Equals(namedType, KnownSymbols.IDictionary))
            {
                // Handle IDictionary using Dictionary<object, object>
                INamedTypeSymbol dictOfObject = KnownSymbols.DictionaryOfTKeyTValue!.Construct(keyType, valueType);
                constructionStrategy = CollectionConstructionStrategy.Mutable;
                implementationType = dictOfObject;
            }
        }
        else if (GetImmutableDictionaryFactory(namedType) is IMethodSymbol factoryMethod)
        {
            constructionStrategy = CollectionConstructionStrategy.Enumerable;
            enumerableFactory = factoryMethod;
        }

        if ((status = IncludeNestedType(keyType, ref ctx)) != TypeDataModelGenerationStatus.Success ||
            (status = IncludeNestedType(valueType, ref ctx)) != TypeDataModelGenerationStatus.Success)
        {
            // return true but a null model to indicate that the type is an unsupported dictionary type
            return true;
        }

        model = new DictionaryDataModel
        {
            Type = type,
            KeyType = keyType,
            ValueType = valueType,
            DictionaryKind = kind,
            ConstructionStrategy = constructionStrategy,
            ImplementationType = implementationType,
            SpanFactory = spanFactory,
            EnumerableFactory = enumerableFactory,
        };

        return true;

        bool ContainsSettableIndexer(ITypeSymbol type, ITypeSymbol keyType, ITypeSymbol valueType)
        {
            return type.GetAllMembers()
                .OfType<IPropertySymbol>()
                .Any(prop =>
                    prop is { IsStatic: false, IsIndexer: true, Parameters.Length: 1, SetMethod: not null } &&
                    SymbolEqualityComparer.Default.Equals(prop.Parameters[0].Type, keyType) &&
                    SymbolEqualityComparer.Default.Equals(prop.Type, valueType) &&
                    IsAccessibleSymbol(prop));
        }

        IMethodSymbol? GetImmutableDictionaryFactory(INamedTypeSymbol namedType)
        {
            if (SymbolEqualityComparer.Default.Equals(namedType.ConstructedFrom, KnownSymbols.ImmutableDictionary))
            {
                return KnownSymbols.Compilation.GetTypeByMetadataName("System.Collections.Immutable.ImmutableDictionary")
                    .GetMethodSymbol(method =>
                        method.IsStatic && method.IsGenericMethod && method.Name is "CreateRange" &&
                        method.Parameters.Length == 1 && method.Parameters[0].Type.Name is "IEnumerable")
                    .MakeGenericMethod(namedType.TypeArguments[0], namedType.TypeArguments[1]);
            }

            if (SymbolEqualityComparer.Default.Equals(namedType.ConstructedFrom, KnownSymbols.ImmutableSortedDictionary))
            {
                return KnownSymbols.Compilation.GetTypeByMetadataName("System.Collections.Immutable.ImmutableSortedDictionary")
                    .GetMethodSymbol(method =>
                        method.IsStatic && method.IsGenericMethod && method.Name is "CreateRange" &&
                        method.Parameters.Length == 1 && method.Parameters[0].Type.Name is "IEnumerable")
                    .MakeGenericMethod(namedType.TypeArguments[0], namedType.TypeArguments[1]);
            }

            return null;
        }
    }
}
