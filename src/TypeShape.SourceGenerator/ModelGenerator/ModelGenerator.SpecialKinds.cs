using Microsoft.CodeAnalysis;
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
        out ITypeSymbol? implementedCollectionType)
    {
        nullableType = null;
        dictionaryType = null;
        enumerableType = null;
        implementedCollectionType = null;

        if ((enumType = MapEnum(typeId, type)) != null)
        {
            return true;
        }

        if ((nullableType = MapNullable(typeId, type)) != null)
        {
            return true;
        }

        if ((dictionaryType = MapDictionary(typeId, type, out implementedCollectionType)) != null)
        {
            return true;
        }

        if ((enumerableType = MapEnumerable(typeId, type, out implementedCollectionType)) != null)
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
            UnderlyingType = GetOrCreateTypeId(((INamedTypeSymbol)type).EnumUnderlyingType!),
        };
    }

    private NullableTypeModel? MapNullable(TypeId typeId, ITypeSymbol type)
    {
        if (type.OriginalDefinition.SpecialType is not SpecialType.System_Nullable_T)
            return null;

        return new NullableTypeModel
        {
            Type = typeId,
            ElementType = GetOrCreateTypeId(((INamedTypeSymbol)type).TypeArguments[0]),
        };
    }

    private EnumerableTypeModel? MapEnumerable(TypeId typeId, ITypeSymbol type, out ITypeSymbol? enumerableInterface)
    {
        ITypeSymbol? elementType = null;
        EnumerableKind kind = default;
        enumerableInterface = null;

        if (type is IArrayTypeSymbol array)
        {
            if (array.Rank > 1)
                throw new NotImplementedException("Multi-dimensional arrays.");

            elementType = array.ElementType;
            kind = EnumerableKind.ArrayOfT;
        }
        else if (
            type.AllInterfaces.FirstOrDefault(i => i.OriginalDefinition.SpecialType is SpecialType.System_Collections_Generic_IEnumerable_T)
            is { } enumerableOfT)
        {
            elementType = enumerableOfT.TypeArguments[0];
            ITypeSymbol? collectionOfT = type.AllInterfaces.FirstOrDefault(i =>
                i.OriginalDefinition.SpecialType is SpecialType.System_Collections_Generic_ICollection_T &&
                SymbolEqualityComparer.Default.Equals(i.TypeArguments[0], elementType));

            enumerableInterface = collectionOfT ?? enumerableOfT;
            kind = collectionOfT is { } ? EnumerableKind.ICollectionOfT : EnumerableKind.IEnumerableOfT;
        }
        else if (
            type.AllInterfaces.FirstOrDefault(i => i.OriginalDefinition.SpecialType is SpecialType.System_Collections_IEnumerable)
            is { } enumerable)
        {
            if (_iList is { } ilist && type.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, ilist)))
            {
                enumerableInterface = ilist;
                kind = EnumerableKind.IList;
            }
            else
            {
                enumerableInterface = enumerable;
                kind = EnumerableKind.IEnumerable;
            }

            elementType = _semanticModel.Compilation.ObjectType;
        }

        if (elementType is null)
        {
            return null;
        }

        TypeId elementTypeId = GetOrCreateTypeId(elementType);

        IMethodSymbol? addMethod = type.GetMembers()
            .OfType<IMethodSymbol>()
            .FirstOrDefault(method =>
                method is { DeclaredAccessibility: Accessibility.Public, IsStatic: false, ReturnsVoid: true,
                            Name: "Add" or "Enqueue" or "Push", Parameters.Length: 1 } &&
                SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, elementType));

        return new EnumerableTypeModel
        {
            Type = typeId,
            ElementType = elementTypeId,
            Kind = kind,
            AddElementMethod = addMethod?.Name
        };
    }

    private DictionaryTypeModel? MapDictionary(TypeId typeId, ITypeSymbol type, out ITypeSymbol? dictionaryInterface)
    {
        dictionaryInterface = null;

        if (_iDictionaryOfTKeyTValue is { } genericIDict &&
            type.AllInterfaces.FirstOrDefault(i => SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, genericIDict)) is { } genericIDictInstance)
        {
            TypeId keyType = GetOrCreateTypeId(genericIDictInstance.TypeArguments[0]);
            TypeId valueType = GetOrCreateTypeId(genericIDictInstance.TypeArguments[1]);
            dictionaryInterface = genericIDictInstance;

            return new DictionaryTypeModel
            {
                Type = typeId,
                KeyType = keyType,
                ValueType = valueType,
                Kind = DictionaryKind.IDictionaryOfKV,
            };
        }
        else if (
            _iReadOnlyDictionaryOfTKeyTValue is { } genericReadOnlyIDict &&
            type.AllInterfaces.FirstOrDefault(i => SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, genericReadOnlyIDict)) is { } genericReadOnlyIDictInstance)
        {
            TypeId keyType = GetOrCreateTypeId(genericReadOnlyIDictInstance.TypeArguments[0]);
            TypeId valueType = GetOrCreateTypeId(genericReadOnlyIDictInstance.TypeArguments[1]);
            dictionaryInterface = genericReadOnlyIDictInstance;

            return new DictionaryTypeModel
            {
                Type = typeId,
                KeyType = keyType,
                ValueType = valueType,
                Kind = DictionaryKind.IReadOnlyDictionaryOfKV,
            };
        }
        else if (
            _iDictionary is { } iDictionary &&
            type.AllInterfaces.FirstOrDefault(i => SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, iDictionary)) is { })
        {
            TypeId objectType = GetOrCreateTypeId(_semanticModel.Compilation.ObjectType);
            dictionaryInterface = iDictionary;

            return new DictionaryTypeModel
            {
                Type = typeId,
                KeyType = objectType,
                ValueType = objectType,
                Kind = DictionaryKind.IDictionary,
            };
        }

        return null;
    }
}
