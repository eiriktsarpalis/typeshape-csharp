using Microsoft.CodeAnalysis;
using System.Diagnostics;
using TypeShape.SourceGenerator.Helpers;
using TypeShape.SourceGenerator.Model;

namespace TypeShape.SourceGenerator;

public sealed partial class ModelGenerator
{
    private ImmutableEquatableArray<PropertyModel> MapProperties(TypeId typeId, ITypeSymbol type, ITypeSymbol[]? classTupleElements, bool disallowMemberResolution)
    {
        if (disallowMemberResolution || type.TypeKind is not (TypeKind.Struct or TypeKind.Class or TypeKind.Interface) || type.SpecialType is not SpecialType.None)
        {
            return ImmutableEquatableArray.Empty<PropertyModel>();
        }

        if (classTupleElements is not null)
        {
            return classTupleElements
                .Select((elementType, i) => MapClassTupleElement(typeId, elementType, i))
                .ToImmutableEquatableArray();
        }

        return ResolvePropertyAndFieldSymbols(type)
            .Select(member => member is IPropertySymbol p ? MapProperty(typeId, p) : MapField(typeId, (IFieldSymbol)member))
            .ToImmutableEquatableArray();
    }

    private IEnumerable<ISymbol> ResolvePropertyAndFieldSymbols(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol namedType)
        {
            yield break;
        }

        if (namedType.IsTupleType)
        {
            foreach (IFieldSymbol symbol in namedType.TupleElements)
            {
                yield return symbol;
            }

            yield break;
        }

        foreach (ITypeSymbol current in namedType.GetSortedTypeHierarchy())
        {
            var members = current.GetMembers()
                .Where(m => m.Kind is SymbolKind.Field or SymbolKind.Property)
                .OrderByDescending(m => m.Kind is SymbolKind.Property); // for consistency with reflection, sort properties ahead of fields
            
            foreach (ISymbol member in members) 
            {
                if (member is IPropertySymbol { IsStatic: false, Parameters: [] } ps &&
                    IsSupportedType(ps.Type) && IsAccessibleFromGeneratedType(ps))
                {
                    yield return ps;
                }
                else if (
                    member is IFieldSymbol { IsStatic: false } fs &&
                    IsSupportedType(fs.Type) && IsAccessibleFromGeneratedType(fs))
                {
                    yield return fs;
                }
            }
        }
    }

    private PropertyModel MapProperty(TypeId typeId, IPropertySymbol property)
    {
        Debug.Assert(!property.IsStatic && !property.IsIndexer);
        property.GetNullableReferenceTypeInfo(out bool isGetterNonNullable, out bool isSetterNonNullable);
        return new PropertyModel
        {
            Name = property.Name,
            UnderlyingMemberName = property.Name,
            DeclaringType = typeId,
            DeclaringInterfaceType = property.ContainingType.TypeKind is TypeKind.Interface ? CreateTypeId(property.ContainingType) : null,
            PropertyType = EnqueueForGeneration(property.Type),
            IsGetterNonNullableReferenceType = isGetterNonNullable,
            IsSetterNonNullableReferenceType = isSetterNonNullable,
            EmitGetter = property.GetMethod is { } getter && IsAccessibleFromGeneratedType(getter),
            EmitSetter = property.SetMethod is IMethodSymbol { IsInitOnly: false } setter && IsAccessibleFromGeneratedType(setter),
            IsField = false,
        };
    }

    private PropertyModel MapField(TypeId typeId, IFieldSymbol field)
    {
        Debug.Assert(!field.IsStatic);
        field.GetNullableReferenceTypeInfo(out bool isGetterNonNullable, out bool isSetterNonNullable);
        return new PropertyModel
        {
            Name = field.Name,
            UnderlyingMemberName = field.Name,
            DeclaringType = typeId,
            DeclaringInterfaceType = null,
            PropertyType = EnqueueForGeneration(field.Type),
            IsGetterNonNullableReferenceType = isGetterNonNullable,
            IsSetterNonNullableReferenceType = isSetterNonNullable,
            EmitGetter = true,
            EmitSetter = !field.IsReadOnly,
            IsField = true,
        };
    }

    private PropertyModel MapClassTupleElement(TypeId typeId, ITypeSymbol element, int index)
    {
        bool isNonNullableReferenceType = element.IsNonNullableReferenceType();
        return new PropertyModel
        {
            Name = $"Item{index + 1}",
            UnderlyingMemberName = $"{string.Join("", Enumerable.Repeat("Rest.", index / 7))}Item{(index % 7) + 1}",
            DeclaringType = typeId,
            DeclaringInterfaceType = null,
            PropertyType = EnqueueForGeneration(element),
            IsGetterNonNullableReferenceType = isNonNullableReferenceType,
            IsSetterNonNullableReferenceType = isNonNullableReferenceType,
            EmitGetter = true,
            EmitSetter = false,
            IsField = false
        };
    }
}
