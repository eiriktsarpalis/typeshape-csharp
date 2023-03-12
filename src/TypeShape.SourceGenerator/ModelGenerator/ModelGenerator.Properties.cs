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

        var list = new List<PropertyModel>();

        // TODO interface hierarchies

        for (ITypeSymbol? current = type; current != null; current = current.BaseType)
        {
            foreach (ISymbol member in current.GetMembers())
            {
                if (member is IPropertySymbol { IsStatic: false, IsIndexer: false, DeclaredAccessibility: Accessibility.Public } ps &&
                    IsSupportedType(ps.Type))
                {
                    list.Add(MapProperty(typeId, ps));
                }
                else if (
                    member is IFieldSymbol { IsStatic: false, DeclaredAccessibility: Accessibility.Public } fs &&
                    IsSupportedType(fs.Type))
                {
                    list.Add(MapField(typeId, fs));
                }
            }
        }

        return list.ToImmutableArrayEq();
    }

    private PropertyModel MapProperty(TypeId typeId, IPropertySymbol property)
    {
        Debug.Assert(!property.IsStatic && !property.IsIndexer);

        // TODO required & init-only member handling as constructor parameters

        return new PropertyModel
        {
            Name = property.Name,
            DeclaringType = typeId,
            PropertyType = GetOrCreateTypeId(property.Type),
            EmitGetter = property.GetMethod != null,
            EmitSetter = property.SetMethod is IMethodSymbol { IsInitOnly: false },
            IsRequired = property.IsRequired,
            IsInitOnly = property.SetMethod is IMethodSymbol { IsInitOnly: true },
        };
    }

    private PropertyModel MapField(TypeId typeId, IFieldSymbol field)
    {
        Debug.Assert(!field.IsStatic);

        // TODO required & init-only member handling as constructor parameters

        _typesToGenerate.Enqueue(field.Type);

        return new PropertyModel
        {
            Name = field.Name,
            DeclaringType = typeId,
            PropertyType = GetOrCreateTypeId(field.Type),
            EmitGetter = true,
            EmitSetter = !field.IsReadOnly,
            IsRequired = field.IsRequired,
            IsInitOnly = field.IsReadOnly,
            IsField = true,
        };
    }
}
