﻿using Microsoft.CodeAnalysis;
using System.Diagnostics;
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
        out EnumerableTypeModel? enumerableType,
        out ITypeSymbol? resolvedCollectionInterface)
    {
        nullableType = null;
        dictionaryType = null;
        enumerableType = null;
        resolvedCollectionInterface = null;

        if ((enumType = MapEnum(typeId, type)) != null)
        {
            return true;
        }

        if ((nullableType = MapNullable(typeId, type)) != null)
        {
            return true;
        }

        if ((dictionaryType = MapDictionary(typeId, type, out resolvedCollectionInterface)) != null)
        {
            return true;
        }

        if ((enumerableType = MapEnumerable(typeId, type, out resolvedCollectionInterface)) != null)
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

    private EnumerableTypeModel? MapEnumerable(TypeId typeId, ITypeSymbol type, out ITypeSymbol? resolvedInterface)
    {
        ITypeSymbol? elementType = null;
        EnumerableKind kind = default;
        resolvedInterface = null;

        if (type.SpecialType is SpecialType.System_String)
        {
            // Do not treat string as an IEnumerable<char> collection.
            return null;
        }

        if (!_knownSymbols.IEnumerable.IsAssignableFrom(type))
        {
            // Type is not IEnumerable
            return null;
        }

        if (type is IArrayTypeSymbol array)
        {
            if (array.Rank > 1)
            {
                throw new NotImplementedException("Multi-dimensional arrays.");
            }

            elementType = array.ElementType;
            kind = EnumerableKind.ArrayOfT;
        }
        else if (type.GetCompatibleGenericBaseType(_knownSymbols.ICollectionOfT) is { } collectionOfT)
        {
            elementType = collectionOfT.TypeArguments[0];
            resolvedInterface = collectionOfT;
            kind = IsImmutableCollection(type) ? EnumerableKind.ImmutableOfT : EnumerableKind.ICollectionOfT;
        }
        else if (type.GetCompatibleGenericBaseType(_knownSymbols.IEnumerableOfT) is { } enumerableOfT)
        {
            elementType = enumerableOfT.TypeArguments[0];
            resolvedInterface = enumerableOfT;
            kind = IsImmutableCollection(type) ? EnumerableKind.ImmutableOfT : EnumerableKind.IEnumerableOfT;
        }
        else if (_knownSymbols.IList.IsAssignableFrom(type))
        {
            elementType = _semanticModel.Compilation.ObjectType;
            resolvedInterface = _knownSymbols.IList;
            kind = EnumerableKind.IList;
        }
        else
        {
            elementType = _semanticModel.Compilation.ObjectType;
            resolvedInterface = _knownSymbols.IEnumerable;
            kind = EnumerableKind.IEnumerable;
        }

        IMethodSymbol? addMethod = null;

        if (kind is not EnumerableKind.ImmutableOfT)
        {
            addMethod = type.GetMembers()
                .OfType<IMethodSymbol>()
                .FirstOrDefault(method =>
                    method is { IsStatic: false, ReturnsVoid: true, Name: "Add" or "Enqueue" or "Push", Parameters.Length: 1 } &&
                    IsAccessibleFromGeneratedType(method) &&
                    SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, elementType));
        }

        return new EnumerableTypeModel
        {
            Type = typeId,
            ElementType = EnqueueForGeneration(elementType),
            Kind = kind,
            AddElementMethod = addMethod?.Name
        };
    }

    private DictionaryTypeModel? MapDictionary(TypeId typeId, ITypeSymbol type, out ITypeSymbol? resolvedInterface)
    {
        ITypeSymbol? keyType = null;
        ITypeSymbol? valueType = null;
        DictionaryKind kind = default;
        resolvedInterface = null;

        if (type.GetCompatibleGenericBaseType(_knownSymbols.IReadOnlyDictionaryOfTKeyTValue) is { } genericReadOnlyIDictInstance)
        {
            keyType = genericReadOnlyIDictInstance.TypeArguments[0];
            valueType = genericReadOnlyIDictInstance.TypeArguments[1];
            kind = DictionaryKind.IReadOnlyDictionaryOfKV;
            resolvedInterface = genericReadOnlyIDictInstance;
        }
        else if (type.GetCompatibleGenericBaseType(_knownSymbols.IDictionaryOfTKeyTValue) is { } genericIDictInstance)
        {
            keyType = genericIDictInstance.TypeArguments[0];
            valueType = genericIDictInstance.TypeArguments[1];
            kind = DictionaryKind.IDictionaryOfKV;
            resolvedInterface = genericIDictInstance;
        }
        else if (!_knownSymbols.IDictionary.IsAssignableFrom(type))
        {
            return null;
        }
        else
        {
            keyType = _semanticModel.Compilation.ObjectType;
            valueType = _semanticModel.Compilation.ObjectType;
            kind = DictionaryKind.IDictionary;
            resolvedInterface = _knownSymbols.IDictionary;
        }

        Debug.Assert(valueType != null);

        bool hasSettableIndexer = type.GetMembers()
            .OfType<IPropertySymbol>()
            .Any(prop =>
                prop is { IsStatic: false, IsIndexer: true, Parameters.Length: 1, SetMethod: not null } &&
                SymbolEqualityComparer.Default.Equals(prop.Parameters[0].Type, keyType) &&
                IsAccessibleFromGeneratedType(prop));

        return new DictionaryTypeModel
        {
            Type = typeId,
            KeyType = EnqueueForGeneration(keyType),
            ValueType = EnqueueForGeneration(valueType!),
            HasSettableIndexer = hasSettableIndexer,
            Kind = kind,
        };
    }

    private bool IsImmutableCollection(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol { OriginalDefinition: INamedTypeSymbol definition })
        {
            return false;
        }

        SymbolEqualityComparer cmp = SymbolEqualityComparer.Default;
        return 
            cmp.Equals(definition, _knownSymbols.ImmutableArray) ||
            cmp.Equals(definition, _knownSymbols.ImmutableList) ||
            cmp.Equals(definition, _knownSymbols.ImmutableStack) ||
            cmp.Equals(definition, _knownSymbols.ImmutableQueue) ||
            cmp.Equals(definition, _knownSymbols.ImmutableHashSet) ||
            cmp.Equals(definition, _knownSymbols.ImmutableSortedSet) ||
            cmp.Equals(definition, _knownSymbols.ImmutableDictionary) ||
            cmp.Equals(definition, _knownSymbols.ImmutableSortedDictionary);
    }
}
