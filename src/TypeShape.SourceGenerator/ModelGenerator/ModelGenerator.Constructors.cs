using Microsoft.CodeAnalysis;
using System.Diagnostics;
using TypeShape.SourceGenerator.Helpers;
using TypeShape.SourceGenerator.Model;

namespace TypeShape.SourceGenerator;

using SettableMember = (ISymbol Member, ITypeSymbol MemberType, bool IsNonNullable, bool IsRequired, bool IsInitOnly);

public sealed partial class ModelGenerator
{
    private ImmutableEquatableArray<ConstructorModel>? MapConstructors(
        TypeId typeId, 
        ITypeSymbol type, 
        ISymbol[] propertiesOrFields,
        ITypeSymbol[]? classTupleElements,
        bool disallowMemberResolution)
    {
        if (disallowMemberResolution || type.TypeKind is not (TypeKind.Struct or TypeKind.Class))
        {
            return null;
        }
        
        if (classTupleElements is not null)
        {
            return MapTupleConstructors(typeId, type, classTupleElements).ToImmutableEquatableArray();
        }

        if (type is INamedTypeSymbol namedType && namedType.IsTupleType)
        {
            return MapTupleConstructors(typeId, namedType, namedType.TupleElements.Select(e => e.Type)).ToImmutableEquatableArray();
        }

        SettableMember[] settableMembers = propertiesOrFields
            .Select(member =>
            {
                member.ResolveNullableAnnotation(out _, out bool isSetterNonNullable);
                if (member is IPropertySymbol { SetMethod: IMethodSymbol setter } prop &&
                    IsAccessibleFromGeneratedType(setter))
                {
                    return new SettableMember(prop, prop.Type, isSetterNonNullable, prop.IsRequired, setter.IsInitOnly);
                }

                if (member is IFieldSymbol { IsReadOnly: false } field)
                {
                    return new SettableMember(field, field.Type, isSetterNonNullable, field.IsRequired, false);
                }

                return default;
            })
            .Where(member => member != default)
            .OrderByDescending(member => member.IsRequired || member.IsInitOnly) // Shift required or init members first
            .ToArray();

        IMethodSymbol[] foundConstructors = type.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(ctor =>
                ctor is { MethodKind: MethodKind.Constructor } &&
                ctor.Parameters.All(p => IsSupportedType(p.Type)) &&
                IsAccessibleFromGeneratedType(ctor))
            // Skip the copy constructor for record types
            .Where(ctor => !(type.IsRecord && ctor.Parameters.Length == 1 && SymbolEqualityComparer.Default.Equals(ctor.Parameters[0].Type, type)))
            .ToArray();

        return foundConstructors
            // Only include the implicit constructor in structs if there are no other constructors
            .Where(ctor => !ctor.IsImplicitlyDeclared || foundConstructors.Length == 1)
            .Select(ctor => MapConstructor(type, typeId, ctor, settableMembers))
            .ToImmutableEquatableArray();
    }

    private ConstructorModel MapConstructor(ITypeSymbol type, TypeId typeId, IMethodSymbol constructor, SettableMember[] settableMembers)
    {
        Debug.Assert(constructor.MethodKind is MethodKind.Constructor || constructor.IsStatic);
        Debug.Assert(IsAccessibleFromGeneratedType(constructor));

        ImmutableEquatableArray<ConstructorParameterModel> parameters = constructor.Parameters
            .Select(MapConstructorParameter)
            .ToImmutableEquatableArray();

        bool setsRequiredMembers = constructor.HasSetsRequiredMembersAttribute();
        bool isDefaultConstructorWithoutRequiredOrInitMembers = 
            parameters.Length == 0 && !settableMembers.Any(m => m.IsRequired || m.IsInitOnly);

        Dictionary<string, IParameterSymbol>? parameterIndex = null;
        List<ConstructorParameterModel>? memberInitializers = null;
        List<ConstructorParameterModel>? optionalProperties = null;
        int position = parameters.Length;

        foreach (SettableMember settableMember in isDefaultConstructorWithoutRequiredOrInitMembers ? [] : settableMembers)
        {
            if (setsRequiredMembers && settableMember.IsRequired)
            {
                // Skip required members if set by the constructor.
                continue;
            }

            if (!settableMember.IsRequired && settableMember.Member.IsAutoProperty() && MatchesConstructorParameter(settableMember))
            {
                // Deduplicate any auto properties whose signature matches a constructor parameter.
                continue;
            }

            TypeId memberTypeId = EnqueueForGeneration(settableMember.MemberType);
            var memberModel = new ConstructorParameterModel
            {
                ParameterType = memberTypeId,
                Name = settableMember.Member.Name,
                Position = position++,
                IsRequired = settableMember.IsRequired,
                Kind = settableMember.IsRequired || settableMember.IsInitOnly
                    ? ParameterKind.RequiredOrInitOnlyMember
                    : ParameterKind.OptionalMember,

                IsNonNullable = settableMember.IsNonNullable,
                IsPublic = settableMember.Member.DeclaredAccessibility is Accessibility.Public,
                IsField = settableMember.Member is IFieldSymbol,
                HasDefaultValue = false,
                DefaultValue = null,
                DefaultValueRequiresCast = false,
            };

            if (memberModel.Kind is ParameterKind.RequiredOrInitOnlyMember)
            {
                // Member must only be set as an object initializer expression
                (memberInitializers ??= []).Add(memberModel);
            }
            else
            {
                // Member can be set optionally post construction
                (optionalProperties ??= []).Add(memberModel);
            }

            bool MatchesConstructorParameter(in SettableMember member)
            {
                parameterIndex ??= constructor.Parameters.ToDictionary(p => p.Name, p => p, StringComparer.Ordinal);
                return parameterIndex.TryGetValue(member.Member.Name, out IParameterSymbol? matchingParameter) &&
                    SymbolEqualityComparer.Default.Equals(member.MemberType, matchingParameter.Type);
            }
        }

        return new ConstructorModel
        {
            DeclaringType = SymbolEqualityComparer.Default.Equals(constructor.ContainingType, type)
                ? typeId
                : CreateTypeId(constructor.ContainingType),

            Parameters = parameters.ToImmutableEquatableArray(),
            RequiredOrInitMembers = memberInitializers?.ToImmutableEquatableArray() ?? [],
            OptionalMembers = optionalProperties?.ToImmutableEquatableArray() ?? [],
            OptionalMemberFlagsType = (optionalProperties?.Count ?? 0) switch
            {
                    0 => OptionalMemberFlagsType.None,
                <=  8 => OptionalMemberFlagsType.Byte,
                <= 16 => OptionalMemberFlagsType.UShort,
                <= 32 => OptionalMemberFlagsType.UInt32,
                <= 64 => OptionalMemberFlagsType.ULong,
                    _ => OptionalMemberFlagsType.BitArray,
            },

            StaticFactoryName = constructor.IsStatic ? constructor.GetFullyQualifiedName() : null,
            IsPublic = constructor.DeclaredAccessibility is Accessibility.Public,
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
            Kind = ParameterKind.ConstructorParameter,
            IsRequired = !parameter.HasExplicitDefaultValue,
            IsNonNullable = parameter.IsNonNullableAnnotation(),
            IsPublic = true,
            IsField = false,
            HasDefaultValue = parameter.HasExplicitDefaultValue,
            DefaultValue = parameter.HasExplicitDefaultValue ? parameter.ExplicitDefaultValue : null,
            DefaultValueRequiresCast = parameter.Type 
                is { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } 
                or { TypeKind: TypeKind.Enum },
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
                RequiredOrInitMembers = [],
                OptionalMembers = [],
                OptionalMemberFlagsType = OptionalMemberFlagsType.None,
                StaticFactoryName = null,
                IsPublic = true,
            };
        }

        yield return new ConstructorModel
        {
            DeclaringType = typeId,
            Parameters = tupleElements.Select(MapTupleConstructorParameter).ToImmutableEquatableArray(),
            RequiredOrInitMembers = [],
            OptionalMembers = [],
            OptionalMemberFlagsType = OptionalMemberFlagsType.None,
            StaticFactoryName = null,
            IsPublic = true,
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
                Kind = ParameterKind.ConstructorParameter,
                IsRequired = true,
                IsPublic = true,
                IsField = true,
                IsNonNullable = tupleElement.IsNonNullableAnnotation(),
                DefaultValue = null,
                DefaultValueRequiresCast = false,
            };
        }
    }
}
