using Microsoft.CodeAnalysis;
using System.Diagnostics;
using TypeShape.SourceGenerator.Helpers;
using TypeShape.SourceGenerator.Model;

namespace TypeShape.SourceGenerator;

public sealed partial class ModelGenerator
{
    private ImmutableEquatableArray<ConstructorModel> MapConstructors(TypeId typeId, ITypeSymbol type, ITypeSymbol[]? classTupleElements, bool disallowMemberResolution)
    {
        if (disallowMemberResolution || type.TypeKind is not (TypeKind.Struct or TypeKind.Class) || type.SpecialType is not SpecialType.None)
        {
            return [];
        }
        
        if (classTupleElements is not null)
        {
            return MapTupleConstructors(typeId, type, classTupleElements).ToImmutableEquatableArray();
        }

        if (type is INamedTypeSymbol namedType && namedType.IsTupleType)
        {
            return MapTupleConstructors(typeId, namedType, namedType.TupleElements.Select(e => e.Type)).ToImmutableEquatableArray();
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
            .Select(ctor => MapConstructor(type, typeId, ctor, requiredOrInitMembers))
            .ToImmutableEquatableArray();
    }

    private ConstructorModel MapConstructor(ITypeSymbol type, TypeId typeId, IMethodSymbol constructor, ConstructorParameterModel[]? requiredOrInitOnlyMembers = null)
    {
        Debug.Assert(constructor.MethodKind is MethodKind.Constructor || constructor.IsStatic);
        Debug.Assert(IsAccessibleFromGeneratedType(constructor));

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

        foreach (ConstructorParameterModel memberInitializer in (requiredOrInitOnlyMembers ?? []))
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
            DeclaringType = SymbolEqualityComparer.Default.Equals(constructor.ContainingType, type)
                ? typeId
                : CreateTypeId(constructor.ContainingType),

            Parameters = parameters.ToImmutableEquatableArray(),
            MemberInitializers = memberInitializers.ToImmutableEquatableArray(),
            StaticFactoryName = constructor.IsStatic ? constructor.GetFullyQualifiedName() : null,
        };
    }

    private ConstructorParameterModel MapConstructorParameter(IParameterSymbol parameter)
    {
        TypeId typeId = EnqueueForGeneration(parameter.Type);
        return new ConstructorParameterModel
        {
            Name = parameter.Name,
            Position = parameter.Ordinal,
            ParameterType = typeId,
            IsRequired = !parameter.HasExplicitDefaultValue,
            IsMemberInitializer = false,
            IsAutoProperty = false,
            IsNonNullable = parameter.IsNonNullableAnnotation(),
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

        TypeId typeId = EnqueueForGeneration(propertySymbol.Type);
        propertySymbol.ResolveNullableAnnotation(out _, out bool isSetterNonNullable);
        return new ConstructorParameterModel
        {
            ParameterType = typeId,
            Name = propertySymbol.Name,
            Position = -1, // must be set relative to each constructor arity
            IsRequired = propertySymbol.IsRequired,
            IsMemberInitializer = true,
            IsAutoProperty = propertySymbol.IsAutoProperty(),
            IsNonNullable = isSetterNonNullable,
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

        TypeId typeId = EnqueueForGeneration(fieldSymbol.Type);
        fieldSymbol.ResolveNullableAnnotation(out _, out bool isSetterNonNullable);
        return new ConstructorParameterModel
        {
            ParameterType = typeId,
            Name = fieldSymbol.Name,
            Position = -1, // must be set relative to each constructor arity
            IsRequired = fieldSymbol.IsRequired,
            IsMemberInitializer = true,
            IsAutoProperty = false,
            IsNonNullable = isSetterNonNullable,
            HasDefaultValue = false,
            DefaultValue = null,
            DefaultValueRequiresCast = false,
        };
    }

    private IEnumerable<ConstructorModel> MapTupleConstructors(TypeId typeId, ITypeSymbol tupleType, IEnumerable<ITypeSymbol> tupleElements)
    {
        if (tupleType.IsValueType)
        {
            // Return the default constructor for value tuples
            yield return new ConstructorModel
            {
                DeclaringType = typeId,
                Parameters = [],
                MemberInitializers = [],
                StaticFactoryName = null,
            };
        }

        yield return new ConstructorModel
        {
            DeclaringType = typeId,
            Parameters = tupleElements.Select(MapTupleConstructorParameter).ToImmutableEquatableArray(),
            MemberInitializers = [],
            StaticFactoryName = null,
        };

        ConstructorParameterModel MapTupleConstructorParameter(ITypeSymbol tupleElement, int position)
        {
            TypeId typeId = EnqueueForGeneration(tupleElement);
            return new ConstructorParameterModel
            { 
                Name = $"Item{position + 1}",
                Position = position,
                ParameterType = typeId,
                HasDefaultValue = false,
                IsRequired = true,
                IsMemberInitializer = false,
                IsNonNullable = tupleElement.IsNonNullableAnnotation(),
                IsAutoProperty = false,
                DefaultValue = null,
                DefaultValueRequiresCast = false,
            };
        }
    }
}
