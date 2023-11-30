using Microsoft.CodeAnalysis;
using System.Diagnostics;
using TypeShape.SourceGenerator.Helpers;
using TypeShape.SourceGenerator.Model;

namespace TypeShape.SourceGenerator;

public sealed partial class ModelGenerator
{
    private ImmutableEquatableArray<PropertyModel> MapProperties(TypeId typeId, ITypeSymbol type, ISymbol[] propertiesOrFields, ITypeSymbol[]? classTupleElements, bool disallowMemberResolution)
    {
        if (disallowMemberResolution || type.TypeKind is not (TypeKind.Struct or TypeKind.Class or TypeKind.Interface) || type.SpecialType is not SpecialType.None)
        {
            return [];
        }

        if (classTupleElements is not null)
        {
            return classTupleElements
                .Select((elementType, i) => MapClassTupleElement(typeId, elementType, i))
                .ToImmutableEquatableArray();
        }

        return propertiesOrFields
            .Select(member => member is IPropertySymbol p ? MapProperty(typeId, p) : MapField(typeId, (IFieldSymbol)member))
            .ToImmutableEquatableArray();
    }

    private IEnumerable<ISymbol> ResolvePropertyAndFieldSymbols(ITypeSymbol type, bool disallowMemberResolution)
    {
        if (disallowMemberResolution || type is not INamedTypeSymbol namedType)
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
        property.ResolveNullableAnnotation(out bool isGetterNonNullable, out bool isSetterNonNullable);
        bool emitGetter = property.GetMethod is { } getter && IsAccessibleFromGeneratedType(getter);
        bool emitSetter = property.SetMethod is IMethodSymbol { IsInitOnly: false } setter && IsAccessibleFromGeneratedType(setter);

        return new PropertyModel
        {
            Name = property.Name,
            UnderlyingMemberName = property.Name,
            DeclaringType = typeId,
            DeclaringInterfaceType = property.ContainingType.TypeKind is TypeKind.Interface ? CreateTypeId(property.ContainingType) : null,
            PropertyType = EnqueueForGeneration(property.Type),
            IsGetterNonNullable = emitGetter && isGetterNonNullable,
            IsSetterNonNullable = emitSetter && isSetterNonNullable,
            EmitGetter = emitGetter,
            EmitSetter = emitSetter,
            IsGetterPublic = emitGetter && property.GetMethod?.DeclaredAccessibility is Accessibility.Public,
            IsSetterPublic = emitSetter && property.SetMethod?.DeclaredAccessibility is Accessibility.Public,
            IsField = false,
        };
    }

    private PropertyModel MapField(TypeId typeId, IFieldSymbol field)
    {
        Debug.Assert(!field.IsStatic);
        field.ResolveNullableAnnotation(out bool isGetterNonNullable, out bool isSetterNonNullable);
        return new PropertyModel
        {
            Name = field.Name,
            UnderlyingMemberName = field.Name,
            DeclaringType = typeId,
            DeclaringInterfaceType = null,
            PropertyType = EnqueueForGeneration(field.Type),
            IsGetterNonNullable = isGetterNonNullable,
            IsSetterNonNullable = !field.IsReadOnly && isSetterNonNullable,
            EmitGetter = true,
            EmitSetter = !field.IsReadOnly,
            IsGetterPublic = field.DeclaredAccessibility is Accessibility.Public,
            IsSetterPublic = field.DeclaredAccessibility is Accessibility.Public,
            IsField = true,
        };
    }

    private PropertyModel MapClassTupleElement(TypeId typeId, ITypeSymbol element, int index)
    {
        return new PropertyModel
        {
            Name = $"Item{index + 1}",
            UnderlyingMemberName = $"{string.Join("", Enumerable.Repeat("Rest.", index / 7))}Item{(index % 7) + 1}",
            DeclaringType = typeId,
            DeclaringInterfaceType = null,
            PropertyType = EnqueueForGeneration(element),
            EmitGetter = true,
            EmitSetter = false,
            IsGetterPublic = true,
            IsSetterPublic = true,
            IsGetterNonNullable = element.IsNonNullableAnnotation(),
            IsSetterNonNullable = false, // No setter is emitted
            IsField = false
        };
    }
}
