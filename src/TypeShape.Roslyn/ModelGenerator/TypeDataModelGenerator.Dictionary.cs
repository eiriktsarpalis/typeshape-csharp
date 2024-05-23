using Microsoft.CodeAnalysis;
using System.Transactions;
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
        CollectionModelConstructionStrategy constructionStrategy = CollectionModelConstructionStrategy.None;
        IMethodSymbol? factoryMethod = null;
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

        if (namedType.Constructors.FirstOrDefault(ctor => ctor.Parameters.Length == 0 && !ctor.IsStatic && IsAccessibleSymbol(ctor)) is { } ctor &&
            ContainsSettableIndexer(type, keyType, valueType))
        {
            constructionStrategy = CollectionModelConstructionStrategy.Mutable;
            factoryMethod = ctor;
        }
        else if (namedType.Constructors.FirstOrDefault(ctor =>
            IsAccessibleSymbol(ctor) &&
            ctor.Parameters is [{ Type: INamedTypeSymbol parameterType }] &&
            SymbolEqualityComparer.Default.Equals(parameterType.ConstructedFrom, KnownSymbols.ReadOnlySpanOfT) &&
            parameterType.TypeArguments is [INamedTypeSymbol elementType] &&
            SymbolEqualityComparer.Default.Equals(elementType.ConstructedFrom, KnownSymbols.KeyValuePairOfKV) &&
            elementType.TypeArguments is [INamedTypeSymbol k, INamedTypeSymbol v] &&
            SymbolEqualityComparer.Default.Equals(k, keyType) &&
            SymbolEqualityComparer.Default.Equals(v, valueType)) is IMethodSymbol ctor2)
        {
            constructionStrategy = CollectionModelConstructionStrategy.Span;
            factoryMethod = ctor2;
        }
        else if (namedType.Constructors.FirstOrDefault(ctor =>
            {
                if (IsAccessibleSymbol(ctor) &&
                    ctor.Parameters is [{ Type: INamedTypeSymbol { IsGenericType: true } parameterType }] &&
                    KnownSymbols.DictionaryOfTKeyTValue?.GetCompatibleGenericBaseType(parameterType.ConstructedFrom) != null)
                {
                    // Constructor accepts a single parameter that is a subtype of Dictionary<,>

                    if (parameterType.TypeArguments is [INamedTypeSymbol k, INamedTypeSymbol v] &&
                        SymbolEqualityComparer.Default.Equals(k, keyType) &&
                        SymbolEqualityComparer.Default.Equals(v, valueType))
                    {
                        // The parameter type is Dictionary<TKey, TValue>, IDictionary<TKey, TValue> or IReadOnlyDictionary<TKey, TValue>
                        return true;
                    }

                    if (parameterType.TypeArguments is [INamedTypeSymbol kvp] &&
                        SymbolEqualityComparer.Default.Equals(kvp.ConstructedFrom, KnownSymbols.KeyValuePairOfKV) &&
                        kvp.TypeArguments is [INamedTypeSymbol k1, INamedTypeSymbol v2] &&
                        SymbolEqualityComparer.Default.Equals(k1, keyType) &&
                        SymbolEqualityComparer.Default.Equals(v2, valueType))
                    {
                        // The parameter type is IEnumerable<KeyValuePair<TKey, TValue>>, ICollection<KeyValuePair<TKey, TValue>> or IReadOnlyCollection<KeyValuePair<TKey, TValue>>
                        return true;
                    }
                }

                return false;
            }) is IMethodSymbol ctor3)
        {
            constructionStrategy = CollectionModelConstructionStrategy.Dictionary;
            factoryMethod = ctor3;
        }

        if (namedType.TypeKind is TypeKind.Interface)
        {
            if (namedType.TypeArguments.Length == 2 && KnownSymbols.DictionaryOfTKeyTValue?.GetCompatibleGenericBaseType(namedType.ConstructedFrom) != null)
            {
                // Handle IDictionary<TKey, TValue> and IReadOnlyDictionary<TKey, TValue> using Dictionary<TKey, TValue>
                INamedTypeSymbol dictOfTKeyTValue = KnownSymbols.DictionaryOfTKeyTValue!.Construct(keyType, valueType);
                constructionStrategy = CollectionModelConstructionStrategy.Mutable;
                factoryMethod = dictOfTKeyTValue.Constructors.FirstOrDefault(ctor => ctor.Parameters.IsEmpty);
            }
            else if (SymbolEqualityComparer.Default.Equals(namedType, KnownSymbols.IDictionary))
            {
                // Handle IDictionary using Dictionary<object, object>
                INamedTypeSymbol dictOfObject = KnownSymbols.DictionaryOfTKeyTValue!.Construct(keyType, valueType);
                constructionStrategy = CollectionModelConstructionStrategy.Mutable;
                factoryMethod = dictOfObject.Constructors.FirstOrDefault(ctor => ctor.Parameters.IsEmpty);
            }
        }
        else if (GetImmutableDictionaryFactory(namedType, out bool isFSharpMap) is IMethodSymbol factory)
        {
            constructionStrategy = isFSharpMap ? CollectionModelConstructionStrategy.TupleEnumerable : CollectionModelConstructionStrategy.Dictionary;
            factoryMethod = factory;
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
            FactoryMethod = factoryMethod,
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

        IMethodSymbol? GetImmutableDictionaryFactory(INamedTypeSymbol namedType, out bool isFSharpMap)
        {
            isFSharpMap = false;
            
            if (SymbolEqualityComparer.Default.Equals(namedType.ConstructedFrom, KnownSymbols.ImmutableDictionary))
            {
                return KnownSymbols.Compilation.GetTypeByMetadataName("System.Collections.Immutable.ImmutableDictionary")
                    .GetMethodSymbol(method =>
                        method is { IsStatic: true, IsGenericMethod: true, Name: "CreateRange", Parameters: [var param] } &&
                        param.Type.Name is "IEnumerable")
                    .MakeGenericMethod(namedType.TypeArguments[0], namedType.TypeArguments[1]);
            }

            if (SymbolEqualityComparer.Default.Equals(namedType.ConstructedFrom, KnownSymbols.ImmutableSortedDictionary))
            {
                return KnownSymbols.Compilation.GetTypeByMetadataName("System.Collections.Immutable.ImmutableSortedDictionary")
                    .GetMethodSymbol(method =>
                        method is { IsStatic: true, IsGenericMethod: true, Name: "CreateRange", Parameters: [var param] } && 
                        param.Type.Name is "IEnumerable")
                    .MakeGenericMethod(namedType.TypeArguments[0], namedType.TypeArguments[1]);
            }
            
            if (SymbolEqualityComparer.Default.Equals(namedType.ConstructedFrom, KnownSymbols.FSharpMap))
            {
                IMethodSymbol? ofSeqMethod = KnownSymbols.Compilation.GetTypeByMetadataName("Microsoft.FSharp.Collections.MapModule")
                    .GetMethodSymbol(method =>
                        method is { IsStatic: true, IsGenericMethod: true, Name: "OfSeq", Parameters: [var param] } && 
                        param.Type.Name is "IEnumerable")
                    .MakeGenericMethod(namedType.TypeArguments[0], namedType.TypeArguments[1]);

                isFSharpMap = ofSeqMethod != null;
                return ofSeqMethod;
            }

            return null;
        }
    }
}
