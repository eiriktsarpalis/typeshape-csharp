using Microsoft.CodeAnalysis;
using System.Diagnostics;
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
            return null;
        }

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

            resolvedInterface = collectionOfT ?? enumerableOfT;
            kind = collectionOfT is { } ? EnumerableKind.ICollectionOfT : EnumerableKind.IEnumerableOfT;
        }
        else if (
            type.AllInterfaces.FirstOrDefault(i => i.OriginalDefinition.SpecialType is SpecialType.System_Collections_IEnumerable)
            is { } enumerable)
        {
            if (_iList is { } ilist && type.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, ilist)))
            {
                resolvedInterface = ilist;
                kind = EnumerableKind.IList;
            }
            else
            {
                resolvedInterface = enumerable;
                kind = EnumerableKind.IEnumerable;
            }

            elementType = _semanticModel.Compilation.ObjectType;
        }

        if (elementType is null)
        {
            return null;
        }

        IMethodSymbol? addMethod = type.GetMembers()
            .OfType<IMethodSymbol>()
            .FirstOrDefault(method =>
                method is { IsStatic: false, ReturnsVoid: true, Name: "Add" or "Enqueue" or "Push", Parameters.Length: 1 } &&
                IsAccessibleFromGeneratedType(method) &&
                SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, elementType));

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

        if (_iReadOnlyDictionaryOfTKeyTValue is { } genericReadOnlyIDict &&
            type.AllInterfaces.FirstOrDefault(i => SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, genericReadOnlyIDict)) is { } genericReadOnlyIDictInstance)
        {
            keyType = genericReadOnlyIDictInstance.TypeArguments[0];
            valueType = genericReadOnlyIDictInstance.TypeArguments[1];
            kind = DictionaryKind.IReadOnlyDictionaryOfKV;
            resolvedInterface = genericReadOnlyIDictInstance;
        }
        else if (
            _iDictionaryOfTKeyTValue is { } genericIDict &&
            type.AllInterfaces.FirstOrDefault(i => SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, genericIDict)) is { } genericIDictInstance)
        {
            keyType = genericIDictInstance.TypeArguments[0];
            valueType = genericIDictInstance.TypeArguments[1];
            kind = DictionaryKind.IDictionaryOfKV;
            resolvedInterface = genericIDictInstance;
        }
        else if (
            _iDictionary is { } iDictionary &&
            type.AllInterfaces.FirstOrDefault(i => SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, iDictionary)) is { })
        {
            keyType = _semanticModel.Compilation.ObjectType;
            valueType = _semanticModel.Compilation.ObjectType;
            kind = DictionaryKind.IDictionary;
            resolvedInterface = iDictionary;
        }

        if (keyType is null)
        {
            return null;
        }

        Debug.Assert(valueType != null);

        bool hasSettableIndexer = type.GetMembers()
            .OfType<IPropertySymbol>()
            .Any(prop =>
                prop is { IsStatic: false, IsIndexer: true, Parameters.Length: 1, SetMethod: { } } &&
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
}
