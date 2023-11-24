using Microsoft.CodeAnalysis;
using System.Diagnostics.CodeAnalysis;
using TypeShape.SourceGenerator.Helpers;
using TypeShape.SourceGenerator.Model;

namespace TypeShape.SourceGenerator;

public sealed partial class ModelGenerator
{
    private bool TryResolveSpecialTypeKinds(
        TypeId typeId, ITypeSymbol type,
        out EnumTypeModel? enumType,
        out NullableTypeModel? nullableType,
        out DictionaryTypeModel? dictionaryType,
        out EnumerableTypeModel? enumerableType)
    {
        nullableType = null;
        dictionaryType = null;
        enumerableType = null;

        if ((enumType = MapEnum(typeId, type)) != null)
        {
            return true;
        }

        if ((nullableType = MapNullable(typeId, type)) != null)
        {
            return true;
        }

        if ((dictionaryType = MapDictionary(typeId, type)) != null)
        {
            return true;
        }

        if ((enumerableType = MapEnumerable(typeId, type)) != null)
        {
            return true;
        }

        return false;
    }

    private EnumTypeModel? MapEnum(TypeId typeId, ITypeSymbol type)
    {
        if (type.TypeKind is not TypeKind.Enum)
            return null;

        return new EnumTypeModel
        {
            Type = typeId,
            UnderlyingType = EnqueueForGeneration(((INamedTypeSymbol)type).EnumUnderlyingType!),
        };
    }

    private NullableTypeModel? MapNullable(TypeId typeId, ITypeSymbol type)
    {
        if (type.OriginalDefinition.SpecialType is not SpecialType.System_Nullable_T)
            return null;

        return new NullableTypeModel
        {
            Type = typeId,
            ElementType = EnqueueForGeneration(((INamedTypeSymbol)type).TypeArguments[0]),
        };
    }

    private EnumerableTypeModel? MapEnumerable(TypeId typeId, ITypeSymbol type)
    {
        if (type.SpecialType is SpecialType.System_String)
        {
            // Do not treat string as an IEnumerable<char> collection.
            return null;
        }

        if (!knownSymbols.IEnumerable.IsAssignableFrom(type))
        {
            // Type is not IEnumerable
            return null;
        }

        CollectionConstructionStrategy constructionStrategy = CollectionConstructionStrategy.None;
        IMethodSymbol? addMethod = null;
        ITypeSymbol? elementType = null;
        EnumerableKind kind = default;
        string? spanFactoryMethod = null;
        int rank = 1;

        if (type is IArrayTypeSymbol array)
        {
            elementType = array.ElementType;

            if (array.Rank == 1)
            {
                kind = EnumerableKind.ArrayOfT;
                constructionStrategy = CollectionConstructionStrategy.Span;
                spanFactoryMethod = null; // Uses Span.ToArray() instance method, filled by the emitter.
            }
            else
            {
                kind = EnumerableKind.MultiDimensionalArrayOfT;
                constructionStrategy = CollectionConstructionStrategy.None;
                rank = array.Rank;
            }
        }
        else if (type is not INamedTypeSymbol namedType)
        {
            return null;
        }
        else if (type.GetCompatibleGenericBaseType(knownSymbols.IEnumerableOfT) is { } enumerableOfT)
        {
            kind = EnumerableKind.IEnumerableOfT;
            elementType = enumerableOfT.TypeArguments[0];

            if (namedType.TryGetCollectionBuilderAttribute(elementType, out IMethodSymbol? builderMethod))
            {
                constructionStrategy = CollectionConstructionStrategy.Span;
                spanFactoryMethod = builderMethod.GetFullyQualifiedName();
            }
            else if (
                namedType.Constructors.Any(ctor => ctor.Parameters.Length == 0 && !ctor.IsStatic && IsAccessibleFromGeneratedType(ctor)) &&
                TryGetAddMethod(type, elementType, out addMethod))
            {
                constructionStrategy = CollectionConstructionStrategy.Mutable;
            }
            else if (namedType.Constructors.Any(ctor =>
                IsAccessibleFromGeneratedType(ctor) &&
                ctor.Parameters is [{ Type: INamedTypeSymbol parameterType }] &&
                parameterType.ConstructedFrom.SpecialType is SpecialType.System_Collections_Generic_IEnumerable_T &&
                SymbolEqualityComparer.Default.Equals(parameterType.TypeArguments[0], elementType)))
            {
                constructionStrategy = CollectionConstructionStrategy.Enumerable;
            }
            else if (namedType.TypeKind is TypeKind.Interface)
            {
                INamedTypeSymbol listOfT = knownSymbols.ListOfT!.Construct(elementType);
                if (namedType.IsAssignableFrom(listOfT))
                {
                    // Handle IEnumerable<T>, ICollection<T>, IList<T>, IReadOnlyCollection<T> and IReadOnlyList<T> types using List<T>
                    IMethodSymbol factory = knownSymbols.Compilation.GetTypeByMetadataName("TypeShape.SourceGenModel.CollectionHelpers")
                        .GetMethodSymbol(method =>
                            method.IsStatic && method.IsGenericMethod && method.Name is "CreateList" &&
                            method.Parameters is [{ Type: INamedTypeSymbol { IsGenericType: true } parameterType }] &&
                            SymbolEqualityComparer.Default.Equals(parameterType.ConstructedFrom, knownSymbols.ReadOnlySpanOfT))
                        .MakeGenericMethod(elementType)!;

                    constructionStrategy = CollectionConstructionStrategy.Span;
                    spanFactoryMethod = factory.GetFullyQualifiedName();
                }

                INamedTypeSymbol hashSetOfT = knownSymbols.HashSetOfT!.Construct(elementType);
                if (namedType.IsAssignableFrom(hashSetOfT))
                {
                    // Handle ISet<T> and IReadOnlySet<T> types using HashSet<T>
                    IMethodSymbol factory = knownSymbols.Compilation.GetTypeByMetadataName("TypeShape.SourceGenModel.CollectionHelpers")
                        .GetMethodSymbol(method =>
                            method.IsStatic && method.IsGenericMethod && method.Name is "CreateHashSet" &&
                            method.Parameters is [{ Type: INamedTypeSymbol { IsGenericType: true } parameterType }] &&
                            SymbolEqualityComparer.Default.Equals(parameterType.ConstructedFrom, knownSymbols.ReadOnlySpanOfT))
                        .MakeGenericMethod(elementType)!;

                    constructionStrategy = CollectionConstructionStrategy.Span;
                    spanFactoryMethod = factory.GetFullyQualifiedName();
                }
            }
        }
        else
        {
            elementType = knownSymbols.Compilation.ObjectType;
            kind = EnumerableKind.IEnumerable;

            if (namedType.Constructors.Any(ctor => ctor.Parameters.Length == 0 && !ctor.IsStatic && IsAccessibleFromGeneratedType(ctor)) &&
                TryGetAddMethod(type, elementType, out addMethod))
            {
                constructionStrategy = CollectionConstructionStrategy.Mutable;
            }
            else if (type.IsAssignableFrom(knownSymbols.IList))
            {
                // Handle construction of IList, ICollection and IEnumerable interfaces using List<object?>
                IMethodSymbol factory = knownSymbols.Compilation.GetTypeByMetadataName("TypeShape.SourceGenModel.CollectionHelpers")
                    .GetMethodSymbol(method =>
                        method.IsStatic && method.IsGenericMethod && method.Name is "CreateList" &&
                            method.Parameters is [{ Type: INamedTypeSymbol { IsGenericType: true } parameterType }] &&
                            SymbolEqualityComparer.Default.Equals(parameterType.ConstructedFrom, knownSymbols.ReadOnlySpanOfT))
                    .MakeGenericMethod(elementType)!;

                constructionStrategy = CollectionConstructionStrategy.Span;
                spanFactoryMethod = factory.GetFullyQualifiedName();
            }
        }

        return new EnumerableTypeModel
        {
            Type = typeId,
            ElementType = EnqueueForGeneration(elementType),
            ConstructionStrategy = constructionStrategy,
            SpanFactoryMethod = spanFactoryMethod,
            AddElementMethod = addMethod?.Name,
            Kind = kind,
            Rank = rank,
        };

        bool TryGetAddMethod(ITypeSymbol type, ITypeSymbol elementType, [NotNullWhen(true)] out IMethodSymbol? result)
        {
            result = type.GetAllMembers()
                .OfType<IMethodSymbol>()
                .FirstOrDefault(method =>
                    method is { IsStatic: false, Name: "Add" or "Enqueue" or "Push", Parameters: [{ Type: ITypeSymbol parameterType }] } &&
                    SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, elementType) &&
                    IsAccessibleFromGeneratedType(method));

            return result != null;
        }
    }

    private DictionaryTypeModel? MapDictionary(TypeId typeId, ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol namedType)
        {
            return null;
        }

        CollectionConstructionStrategy constructionStrategy = CollectionConstructionStrategy.None;
        DictionaryKind kind = default;
        string? enumerableFactoryMethod = null;
        string? spanFactoryMethod = null;
        bool hasSettableIndexer = false;
        ITypeSymbol? keyType = null;
        ITypeSymbol? valueType = null;

        if (type.GetCompatibleGenericBaseType(knownSymbols.IReadOnlyDictionaryOfTKeyTValue) is { } genericReadOnlyIDictInstance)
        {
            keyType = genericReadOnlyIDictInstance.TypeArguments[0]; 
            valueType = genericReadOnlyIDictInstance.TypeArguments[1];
            kind = DictionaryKind.IReadOnlyDictionaryOfKV;
        }
        else if (type.GetCompatibleGenericBaseType(knownSymbols.IDictionaryOfTKeyTValue) is { } genericIDictInstance)
        {
            keyType = genericIDictInstance.TypeArguments[0];
            valueType = genericIDictInstance.TypeArguments[1];
            kind = DictionaryKind.IDictionaryOfKV;
        }
        else if (knownSymbols.IDictionary.IsAssignableFrom(type))
        {
            keyType = knownSymbols.Compilation.ObjectType;
            valueType = knownSymbols.Compilation.ObjectType;
            kind = DictionaryKind.IDictionary;
        }
        else
        {
            return null; // Not a dictionary type
        }

        if (namedType.Constructors.Any(ctor => ctor.Parameters.Length == 0 && !ctor.IsStatic && IsAccessibleFromGeneratedType(ctor)) &&
            HasAddMethod(type, keyType, valueType, out hasSettableIndexer))
        {
            constructionStrategy = CollectionConstructionStrategy.Mutable;
        }
        else if (namedType.Constructors.Any(ctor =>
            IsAccessibleFromGeneratedType(ctor) &&
            ctor.Parameters is [{ Type: INamedTypeSymbol parameterType }] &&
            SymbolEqualityComparer.Default.Equals(parameterType.ConstructedFrom, knownSymbols.IEnumerableOfT) &&
            parameterType.TypeArguments is [INamedTypeSymbol elementType] &&
            SymbolEqualityComparer.Default.Equals(elementType.ConstructedFrom, knownSymbols.KeyValuePairOfKV) &&
            elementType.TypeArguments is [INamedTypeSymbol k, INamedTypeSymbol v] &&
            SymbolEqualityComparer.Default.Equals(k, keyType) &&
            SymbolEqualityComparer.Default.Equals(v, valueType)))
        {
            constructionStrategy = CollectionConstructionStrategy.Enumerable;
        }

        if (namedType.TypeKind is TypeKind.Interface)
        {
            if (namedType.TypeArguments.Length == 2 && knownSymbols.DictionaryOfTKeyTValue?.GetCompatibleGenericBaseType(namedType.ConstructedFrom) != null)
            {
                // Handle IDictionary<TKey, TValue> and IReadOnlyDictionary<TKey, TValue> using Dictionary<TKey, TValue>
                IMethodSymbol factory = knownSymbols.Compilation.GetTypeByMetadataName("TypeShape.SourceGenModel.CollectionHelpers")
                    .GetMethodSymbol(method =>
                        method.IsStatic && method.IsGenericMethod && method.Name is "CreateDictionary" &&
                            method.Parameters is [{ Type: INamedTypeSymbol { IsGenericType: true } parameterType }] &&
                            SymbolEqualityComparer.Default.Equals(parameterType.ConstructedFrom, knownSymbols.ReadOnlySpanOfT))
                    .MakeGenericMethod(keyType, valueType)!;

                constructionStrategy = CollectionConstructionStrategy.Span;
                spanFactoryMethod = factory.GetFullyQualifiedName();
            }
            else if (SymbolEqualityComparer.Default.Equals(namedType, knownSymbols.IDictionary))
            {
                // Handle IDictionary using Dictionary<object, object>
                IMethodSymbol factory = knownSymbols.Compilation.GetTypeByMetadataName("TypeShape.SourceGenModel.CollectionHelpers")
                    .GetMethodSymbol(method =>
                        method.IsStatic && method.IsGenericMethod && method.Name is "CreateDictionary" &&
                            method.Parameters is [{ Type: INamedTypeSymbol { IsGenericType: true } parameterType }] &&
                            SymbolEqualityComparer.Default.Equals(parameterType.ConstructedFrom, knownSymbols.ReadOnlySpanOfT))
                    .MakeGenericMethod(keyType, valueType)!;

                constructionStrategy = CollectionConstructionStrategy.Span;
                spanFactoryMethod = factory.GetFullyQualifiedName();
            }
        }
        else if (GetImmutableDictionaryFactory(type) is IMethodSymbol factoryMethod)
        {
            constructionStrategy = CollectionConstructionStrategy.Enumerable;
            enumerableFactoryMethod = factoryMethod.GetFullyQualifiedName();
        }

        return new DictionaryTypeModel
        {
            Type = typeId,
            KeyType = EnqueueForGeneration(keyType),
            ValueType = EnqueueForGeneration(valueType!),
            ConstructionStrategy = constructionStrategy,
            HasSettableIndexer = hasSettableIndexer,
            EnumerableFactoryMethod = enumerableFactoryMethod,
            SpanFactoryMethod = spanFactoryMethod,
            Kind = kind,
        };

        bool HasAddMethod(ITypeSymbol type, ITypeSymbol keyType, ITypeSymbol valueType, out bool hasSettableIndexer)
        {
            hasSettableIndexer = type.GetAllMembers()
                .OfType<IPropertySymbol>()
                .Any(prop =>
                    prop is { IsStatic: false, IsIndexer: true, Parameters.Length: 1, SetMethod: not null } &&
                    SymbolEqualityComparer.Default.Equals(prop.Parameters[0].Type, keyType) &&
                    IsAccessibleFromGeneratedType(prop));

            return hasSettableIndexer || type.GetAllMembers()
                .OfType<IMethodSymbol>()
                .Any(method =>
                    method is { IsStatic: false, Name: "Add", Parameters: [IParameterSymbol key, IParameterSymbol value] } &&
                    SymbolEqualityComparer.Default.Equals(key.Type, keyType) &&
                    SymbolEqualityComparer.Default.Equals(value.Type, valueType) &&
                    IsAccessibleFromGeneratedType(method));
        }

        IMethodSymbol? GetImmutableDictionaryFactory(ITypeSymbol type)
        {
            if (SymbolEqualityComparer.Default.Equals(namedType.ConstructedFrom, knownSymbols.ImmutableDictionary))
            {
                return knownSymbols.Compilation.GetTypeByMetadataName("System.Collections.Immutable.ImmutableDictionary")
                    .GetMethodSymbol(method =>
                        method.IsStatic && method.IsGenericMethod && method.Name is "CreateRange" &&
                        method.Parameters.Length == 1 && method.Parameters[0].Type.Name is "IEnumerable")
                    .MakeGenericMethod(namedType.TypeArguments[0], namedType.TypeArguments[1]);
            }

            if (SymbolEqualityComparer.Default.Equals(namedType.ConstructedFrom, knownSymbols.ImmutableSortedDictionary))
            {
                return knownSymbols.Compilation.GetTypeByMetadataName("System.Collections.Immutable.ImmutableSortedDictionary")
                    .GetMethodSymbol(method =>
                        method.IsStatic && method.IsGenericMethod && method.Name is "CreateRange" &&
                        method.Parameters.Length == 1 && method.Parameters[0].Type.Name is "IEnumerable")
                    .MakeGenericMethod(namedType.TypeArguments[0], namedType.TypeArguments[1]);
            }

            return null;
        }
    }
}
