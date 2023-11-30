using Microsoft.CodeAnalysis;
using System.Diagnostics;
using TypeShape.SourceGenerator.Helpers;
using TypeShape.SourceGenerator.Model;

namespace TypeShape.SourceGenerator;

using RequiredOrInitMember = (ISymbol Member, ITypeSymbol MemberType, bool IsNonNullable, bool IsRequired);

public sealed partial class ModelGenerator
{
    private ImmutableEquatableArray<ConstructorModel> MapConstructors(
        TypeId typeId, 
        ITypeSymbol type, 
        ISymbol[] propertiesOrFields,
        ITypeSymbol[]? classTupleElements,
        bool disallowMemberResolution)
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

        RequiredOrInitMember[] requiredOrInitMembers = propertiesOrFields
            .Select(member =>
            {
                if (member is IPropertySymbol { SetMethod: IMethodSymbol setter } prop &&
                    (prop.IsRequired || setter.IsInitOnly) && IsAccessibleFromGeneratedType(setter))
                {
                    prop.ResolveNullableAnnotation(out _, out bool isSetterNonNullable);
                    return new RequiredOrInitMember(prop, prop.Type, isSetterNonNullable, prop.IsRequired);
                }

                if (member is IFieldSymbol { IsRequired: true } field)
                {
                    field.ResolveNullableAnnotation(out _, out bool isSetterNonNullable);
                    return new RequiredOrInitMember(field, field.Type, isSetterNonNullable, field.IsRequired);
                }

                return default;
            })
            .Where(member => member != default)
            .ToArray();

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

    private ConstructorModel MapConstructor(ITypeSymbol type, TypeId typeId, IMethodSymbol constructor, RequiredOrInitMember[] requiredOrInitMembers)
    {
        Debug.Assert(constructor.MethodKind is MethodKind.Constructor || constructor.IsStatic);
        Debug.Assert(IsAccessibleFromGeneratedType(constructor));

        ImmutableEquatableArray<ConstructorParameterModel> parameters = constructor.Parameters
            .Select(MapConstructorParameter)
            .ToImmutableEquatableArray();

        var memberInitializers = new List<ConstructorParameterModel>();
        bool setsRequiredMembers = constructor.HasSetsRequiredMembersAttribute();
        int position = parameters.Length;

        foreach (RequiredOrInitMember memberInitializer in requiredOrInitMembers)
        {
            if (setsRequiredMembers && memberInitializer.IsRequired)
            {
                continue;
            }

            if (!memberInitializer.IsRequired && memberInitializer.Member.IsAutoProperty() && MatchesConstructorParameter(memberInitializer))
            {
                // Deduplicate any auto properties whose signature matches a constructor parameter.
                continue;
            }

            if (memberInitializer.Member is IPropertySymbol property)
            {
                memberInitializers.Add(MapPropertyInitializer(property,memberInitializer.IsNonNullable, position++));
            }
            else if (memberInitializer.Member is IFieldSymbol field)
            {
                memberInitializers.Add(MapFieldInitializer(field, memberInitializer.IsNonNullable, position++));
            }

            bool MatchesConstructorParameter(in RequiredOrInitMember parameter)
            {
                foreach (IParameterSymbol ctorParameter in constructor.Parameters)
                {
                    if (parameter.Member.Name.Equals(ctorParameter.Name, StringComparison.Ordinal) && 
                        SymbolEqualityComparer.Default.Equals(parameter.MemberType, ctorParameter.Type))
                    {
                        return true;
                    }
                }

                return false;
            }
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
            IsMemberInitializer = false,
            IsRequired = !parameter.HasExplicitDefaultValue,
            IsNonNullable = parameter.IsNonNullableAnnotation(),
            HasDefaultValue = parameter.HasExplicitDefaultValue,
            DefaultValue = parameter.HasExplicitDefaultValue ? parameter.ExplicitDefaultValue : null,
            DefaultValueRequiresCast = parameter.Type 
                is { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } 
                or { TypeKind: TypeKind.Enum },
        };
    }

    private ConstructorParameterModel MapPropertyInitializer(IPropertySymbol propertySymbol, bool isNonNullable, int position)
    {
        Debug.Assert(propertySymbol.SetMethod != null);
        TypeId typeId = EnqueueForGeneration(propertySymbol.Type);
        return new ConstructorParameterModel
        {
            ParameterType = typeId,
            Name = propertySymbol.Name,
            Position = position,
            IsRequired = propertySymbol.IsRequired,
            IsMemberInitializer = true,
            IsNonNullable = isNonNullable,
            HasDefaultValue = false,
            DefaultValue = null,
            DefaultValueRequiresCast = false,
        };
    }

    private ConstructorParameterModel MapFieldInitializer(IFieldSymbol fieldSymbol, bool isNonNullable, int position)
    {
        TypeId typeId = EnqueueForGeneration(fieldSymbol.Type);
        return new ConstructorParameterModel
        {
            ParameterType = typeId,
            Name = fieldSymbol.Name,
            Position = position,
            IsMemberInitializer = true,
            IsRequired = fieldSymbol.IsRequired,
            IsNonNullable = isNonNullable,
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
                IsMemberInitializer = false,
                IsRequired = true,
                IsNonNullable = tupleElement.IsNonNullableAnnotation(),
                DefaultValue = null,
                DefaultValueRequiresCast = false,
            };
        }
    }
}
