using Microsoft.CodeAnalysis;
using System.Diagnostics;
using TypeShape.SourceGenerator.Helpers;
using TypeShape.SourceGenerator.Model;

namespace TypeShape.SourceGenerator;

public sealed partial class ModelGenerator
{
    private ImmutableArrayEq<PropertyModel> MapProperties(TypeId typeId, ITypeSymbol type, bool isSpecialTypeKind)
    {
        if (isSpecialTypeKind || type.TypeKind is not (TypeKind.Struct or TypeKind.Class or TypeKind.Interface) || type.SpecialType is not SpecialType.None)
        {
            return ImmutableArrayEq<PropertyModel>.Empty;
        }

        return ResolvePropertyAndFieldSymbols(type)
            .Select(member => member is IPropertySymbol p ? MapProperty(typeId, p) : MapField(typeId, (IFieldSymbol)member))
            .ToImmutableArrayEq();
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
                if (member is IPropertySymbol { IsStatic: false, IsIndexer: false } ps &&
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
        return new PropertyModel
        {
            Name = property.Name,
            DeclaringType = typeId,
            DeclaringInterfaceType = property.ContainingType.TypeKind is TypeKind.Interface ? CreateTypeId(property.ContainingType) : null,
            PropertyType = EnqueueForGeneration(property.Type),
            EmitGetter = property.GetMethod is { } getter && IsAccessibleFromGeneratedType(getter),
            EmitSetter = property.SetMethod is IMethodSymbol { IsInitOnly: false } setter && IsAccessibleFromGeneratedType(setter),
        };
    }

    private PropertyModel MapField(TypeId typeId, IFieldSymbol field)
    {
        Debug.Assert(!field.IsStatic);
        return new PropertyModel
        {
            Name = field.Name,
            DeclaringType = typeId,
            DeclaringInterfaceType = null,
            PropertyType = EnqueueForGeneration(field.Type),
            EmitGetter = true,
            EmitSetter = !field.IsReadOnly,
            IsField = true,
        };
    }
}
