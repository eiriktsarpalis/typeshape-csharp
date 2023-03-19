using Microsoft.CodeAnalysis;
using System.Diagnostics;
using TypeShape.SourceGenerator.Helpers;
using TypeShape.SourceGenerator.Model;

namespace TypeShape.SourceGenerator;

public sealed partial class ModelGenerator
{
    private ImmutableArrayEq<ConstructorModel> MapConstructors(TypeId typeId, ITypeSymbol type, ITypeSymbol? collectionInterface)
    {
        if (type.TypeKind is not (TypeKind.Struct or TypeKind.Class) || type.SpecialType is not SpecialType.None)
        {
            return ImmutableArrayEq<ConstructorModel>.Empty;
        }

        ConstructorParameterModel[] requiredOrInitMembers = ResolvePropertyAndFieldSymbols(type)
            .Select(member => member is IPropertySymbol p ? MapPropertyInitializer(p) : MapFieldInitializer((IFieldSymbol)member))
            .Where(member => member != null)
            .ToArray()!;

        return type.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(ctor =>
                ctor is { MethodKind: MethodKind.Constructor } &&
                ctor.Parameters.All(p => IsSupportedType(p.Type)) &&
                IsAccessibleFromGeneratedType(ctor))
            .Where(ctor => 
                // Skip the copy constructor for record types
                !(type.IsRecord && ctor.Parameters.Length == 1 && SymbolEqualityComparer.Default.Equals(ctor.Parameters[0].Type, type)))
            .Where(ctor =>
                // For collection types only emit the default & interface copy constructors
                collectionInterface is null ||
                ctor.Parameters.Length == 0 ||
                ctor.Parameters.Length == 1 && SymbolEqualityComparer.Default.Equals(ctor.Parameters[0].Type, collectionInterface))
            .Select(ctor => MapConstructor(typeId, ctor, requiredOrInitMembers))
            .ToImmutableArrayEq();
    }

    private ConstructorModel MapConstructor(TypeId typeId, IMethodSymbol constructor, ConstructorParameterModel[] requiredOrInitOnlyMembers)
    {
        Debug.Assert(constructor.MethodKind is MethodKind.Constructor && IsAccessibleFromGeneratedType(constructor));

        var parameters = new List<ConstructorParameterModel>();
        foreach (IParameterSymbol param in constructor.Parameters)
        {
            parameters.Add(MapConstructorParameter(param));
        }

        var memberInitializers = new List<ConstructorParameterModel>();
        bool setsRequiredMembers = constructor.HasSetsRequiredMembersAttribute();
        int position = parameters.Count;

        HashSet<(string FQN, string name)>? ctorParameterSet = constructor.ContainingType.IsRecord
            ? new(parameters.Select(paramModel => (paramModel.ParameterType.FullyQualifiedName, paramModel.Name)))
            : null;

        foreach (ConstructorParameterModel memberInitializer in requiredOrInitOnlyMembers)
        {
            if (setsRequiredMembers && memberInitializer.IsRequired)
            {
                continue;
            }

            if (!memberInitializer.IsRequired && memberInitializer.IsAutoProperty && 
                ctorParameterSet?.Contains((memberInitializer.ParameterType.FullyQualifiedName, memberInitializer.Name)) == true)
            {
                // In records, deduplicate any init auto-properties whose signature matches the constructor parameters.
                continue;
            }

            memberInitializers.Add(memberInitializer with { Position = position++ });
        }

        return new ConstructorModel
        {
            DeclaringType = typeId,
            Parameters = parameters.ToImmutableArrayEq(),
            MemberInitializers = memberInitializers.ToImmutableArrayEq(),
        };
    }

    private ConstructorParameterModel MapConstructorParameter(IParameterSymbol parameter)
    {
        TypeId typeId = GetOrCreateTypeId(parameter.Type);
        return new ConstructorParameterModel
        {
            Name = parameter.Name,
            Position = parameter.Ordinal,
            ParameterType = typeId,
            IsRequired = true,
            IsMemberInitializer = false,
            IsAutoProperty = false,
            HasDefaultValue = parameter.HasExplicitDefaultValue,
            DefaultValue = parameter.HasExplicitDefaultValue ? parameter.ExplicitDefaultValue : null,
            DefaultValueRequiresCast = parameter.Type 
                is { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } 
                or { TypeKind: TypeKind.Enum },
        };
    }

    private ConstructorParameterModel? MapPropertyInitializer(IPropertySymbol propertySymbol)
    {
        if (!propertySymbol.IsRequired && propertySymbol.SetMethod?.IsInitOnly != true)
        {
            return null;
        }

        TypeId typeId = GetOrCreateTypeId(propertySymbol.Type);
        return new ConstructorParameterModel
        {
            ParameterType = typeId,
            Name = propertySymbol.Name,
            Position = -1, // must be set relative to each constructor arity
            IsRequired = propertySymbol.IsRequired,
            IsMemberInitializer = true,
            IsAutoProperty = propertySymbol.IsAutoProperty(),
            HasDefaultValue = false,
            DefaultValue = null,
            DefaultValueRequiresCast = false,
        };
    }

    private ConstructorParameterModel? MapFieldInitializer(IFieldSymbol fieldSymbol)
    {
        if (!fieldSymbol.IsRequired)
        {
            return null;
        }

        TypeId typeId = GetOrCreateTypeId(fieldSymbol.Type);
        return new ConstructorParameterModel
        {
            ParameterType = typeId,
            Name = fieldSymbol.Name,
            Position = -1, // must be set relative to each constructor arity
            IsRequired = fieldSymbol.IsRequired,
            IsMemberInitializer = true,
            IsAutoProperty = false,
            HasDefaultValue = false,
            DefaultValue = null,
            DefaultValueRequiresCast = false,
        };
    }
}
