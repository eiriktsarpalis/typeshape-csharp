using Microsoft.CodeAnalysis;
using TypeShape.Roslyn;
using TypeShape.SourceGenerator.Helpers;
using TypeShape.SourceGenerator.Model;

namespace TypeShape.SourceGenerator;

public sealed partial class Parser
{
    private static TypeShapeModel MapModel(TypeId typeId, TypeDataModel model, bool isGeneratedViaWitnessType)
    {
        bool emitGenericProviderImplementation = isGeneratedViaWitnessType && model.IsRootType;
        return model switch
        {
            EnumDataModel enumModel => new EnumShapeModel
            {
                Type = typeId,
                UnderlyingType = CreateTypeId(enumModel.UnderlyingType),
                EmitGenericTypeShapeProviderImplementation = emitGenericProviderImplementation,
            },

            NullableDataModel nullableModel => new NullableShapeModel
            {
                Type = typeId,
                ElementType = CreateTypeId(nullableModel.ElementType),
                EmitGenericTypeShapeProviderImplementation = emitGenericProviderImplementation,
            },

            EnumerableDataModel enumerableModel => new EnumerableShapeModel
            {
                Type = typeId,
                ElementType = CreateTypeId(enumerableModel.ElementType),
                ConstructionStrategy = enumerableModel.ConstructionStrategy switch
                {
                    _ when enumerableModel.EnumerableKind is EnumerableKind.ArrayOfT or EnumerableKind.MemoryOfT or EnumerableKind.ReadOnlyMemoryOfT
                        => CollectionConstructionStrategy.Span, // use ReadOnlySpan.ToArray() to create the collection

                    CollectionModelConstructionStrategy.Mutable => CollectionConstructionStrategy.Mutable,
                    CollectionModelConstructionStrategy.Span => CollectionConstructionStrategy.Span,
                    CollectionModelConstructionStrategy.List => 
                        IsFactoryAcceptingIEnumerable(enumerableModel.FactoryMethod) 
                        ? CollectionConstructionStrategy.Enumerable 
                        : CollectionConstructionStrategy.Span,

                    _ => CollectionConstructionStrategy.None,
                },

                AddElementMethod = enumerableModel.AddElementMethod?.Name,
                ImplementationTypeFQN = 
                    enumerableModel.ConstructionStrategy is CollectionModelConstructionStrategy.Mutable &&
                    enumerableModel.FactoryMethod is { IsStatic: false, ContainingType: INamedTypeSymbol implType } && 
                    !SymbolEqualityComparer.Default.Equals(implType, enumerableModel.Type)

                    ? implType.GetFullyQualifiedName()
                    : null,

                StaticFactoryMethod = enumerableModel.FactoryMethod is { IsStatic: true } m ? m.GetFullyQualifiedName() : null,
                CtorRequiresListConversion = 
                    enumerableModel.ConstructionStrategy is CollectionModelConstructionStrategy.List &&
                    !IsFactoryAcceptingIEnumerable(enumerableModel.FactoryMethod),

                Kind = enumerableModel.EnumerableKind,
                Rank = enumerableModel.Rank,
                EmitGenericTypeShapeProviderImplementation = emitGenericProviderImplementation,
            },

            DictionaryDataModel dictionaryModel => new DictionaryShapeModel
            {
                Type = typeId,
                KeyType = CreateTypeId(dictionaryModel.KeyType),
                ValueType = CreateTypeId(dictionaryModel.ValueType),
                ConstructionStrategy = dictionaryModel.ConstructionStrategy switch
                {
                    CollectionModelConstructionStrategy.Mutable => CollectionConstructionStrategy.Mutable,
                    CollectionModelConstructionStrategy.Span => CollectionConstructionStrategy.Span,
                    CollectionModelConstructionStrategy.Dictionary =>
                        IsFactoryAcceptingIEnumerable(dictionaryModel.FactoryMethod)
                        ? CollectionConstructionStrategy.Enumerable
                        : CollectionConstructionStrategy.Span,

                    _ => CollectionConstructionStrategy.None,
                },

                ImplementationTypeFQN =
                    dictionaryModel.ConstructionStrategy is CollectionModelConstructionStrategy.Mutable &&
                    dictionaryModel.FactoryMethod is { IsStatic: false, ContainingType: INamedTypeSymbol implType } &&
                    !SymbolEqualityComparer.Default.Equals(implType, dictionaryModel.Type)

                    ? implType.GetFullyQualifiedName()
                    : null,

                StaticFactoryMethod = dictionaryModel.FactoryMethod is { IsStatic: true } m ? m.GetFullyQualifiedName() : null,
                Kind = dictionaryModel.DictionaryKind,
                EmitGenericTypeShapeProviderImplementation = emitGenericProviderImplementation,
                CtorRequiresDictionaryConversion =
                    dictionaryModel.ConstructionStrategy is CollectionModelConstructionStrategy.Dictionary &&
                    !IsFactoryAcceptingIEnumerable(dictionaryModel.FactoryMethod),
            },

            ObjectDataModel objectModel => new ObjectShapeModel
            {
                Type = typeId,
                Constructors = objectModel.Constructors
                    .Select(c => MapConstructor(objectModel.Type, typeId, c))
                    .ToImmutableEquatableArray(),

                Properties = objectModel.Properties
                    .Select(p => MapProperty(model.Type, typeId, p))
                    .ToImmutableEquatableArray(),

                IsValueTupleType = false,
                IsClassTupleType = false,
                IsRecord = model.Type.IsRecord,
                EmitGenericTypeShapeProviderImplementation = emitGenericProviderImplementation,
            },

            TupleDataModel tupleModel => new ObjectShapeModel
            {
                Type = typeId,
                Constructors = MapTupleConstructors(typeId, tupleModel)
                    .ToImmutableEquatableArray(),

                Properties = tupleModel.Elements
                    .Select((e, i) => MapProperty(model.Type, typeId, e, tupleElementIndex: i, isClassTupleType: !tupleModel.IsValueTuple))
                    .ToImmutableEquatableArray(),

                IsValueTupleType = tupleModel.IsValueTuple,
                IsClassTupleType = !tupleModel.IsValueTuple,
                IsRecord = false,
                EmitGenericTypeShapeProviderImplementation = emitGenericProviderImplementation,
            },

            _ => new TypeShapeModel
            { 
                Type = typeId,
                EmitGenericTypeShapeProviderImplementation = emitGenericProviderImplementation,
            }
        };

        static bool IsFactoryAcceptingIEnumerable(IMethodSymbol? method)
        {
            return method?.Parameters is [{ Type: INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Collections_Generic_IEnumerable_T } }];
        }
    }

    private static PropertyShapeModel MapProperty(ITypeSymbol parentType, TypeId parentTypeId, PropertyDataModel property, bool isClassTupleType = false, int tupleElementIndex = -1)
    {
        return new PropertyShapeModel
        {
            Name = isClassTupleType ? $"Item{tupleElementIndex + 1}" : property.Name,
            UnderlyingMemberName = isClassTupleType
                ? $"{string.Join("", Enumerable.Repeat("Rest.", tupleElementIndex / 7))}Item{(tupleElementIndex % 7) + 1}"
                : property.Name,

            DeclaringType = SymbolEqualityComparer.Default.Equals(parentType, property.DeclaringType) ? parentTypeId : CreateTypeId(property.DeclaringType),
            PropertyType = CreateTypeId(property.PropertyType),
            IsGetterNonNullable = property.CanRead && property.IsGetterNonNullable,
            IsSetterNonNullable = property.CanWrite && property.IsSetterNonNullable,
            EmitGetter = property.CanRead,
            EmitSetter = property.CanWrite,
            IsGetterPublic = property.CanRead && property.PropertySymbol is IPropertySymbol { GetMethod.DeclaredAccessibility: Accessibility.Public } or IFieldSymbol { DeclaredAccessibility: Accessibility.Public },
            IsSetterPublic = property.CanWrite && property.PropertySymbol is IPropertySymbol { SetMethod.DeclaredAccessibility: Accessibility.Public } or IFieldSymbol { DeclaredAccessibility: Accessibility.Public },
            IsField = property.IsField,
        };
    }

    private static ConstructorShapeModel MapConstructor(ITypeSymbol declaringType, TypeId declaringTypeId, ConstructorDataModel constructor)
    {
        int position = constructor.Parameters.Length;
        List<ConstructorParameterShapeModel>? requiredOrInitMembers = null;
        List<ConstructorParameterShapeModel>? optionalProperties = null;

        foreach (PropertyDataModel propertyModel in constructor.MemberInitializers.OrderByDescending(p => p.IsRequired || p.IsInitOnly))
        {
            var memberInitializer = new ConstructorParameterShapeModel
            {
                ParameterType = CreateTypeId(propertyModel.PropertyType),
                DeclaringType = SymbolEqualityComparer.Default.Equals(propertyModel.DeclaringType, declaringType)
                    ? declaringTypeId
                    : CreateTypeId(propertyModel.DeclaringType),

                Name = propertyModel.Name,
                Position = position++,
                IsRequired = propertyModel.IsRequired,
                Kind = propertyModel.IsRequired || propertyModel.IsInitOnly
                    ? ParameterKind.RequiredOrInitOnlyMember
                    : ParameterKind.OptionalMember,

                IsNonNullable = propertyModel.IsSetterNonNullable,
                IsPublic = propertyModel.PropertySymbol.DeclaredAccessibility is Accessibility.Public,
                IsField = propertyModel.IsField,
                HasDefaultValue = false,
                DefaultValueExpr = null,
            };

            if (memberInitializer.Kind is ParameterKind.RequiredOrInitOnlyMember)
            {
                // Member must only be set as an object initializer expression
                (requiredOrInitMembers ??= []).Add(memberInitializer);
            }
            else
            {
                // Member can be set optionally post construction
                (optionalProperties ??= []).Add(memberInitializer);
            }
        }

        return new ConstructorShapeModel
        {
            DeclaringType = SymbolEqualityComparer.Default.Equals(constructor.DeclaringType, declaringType)
                ? declaringTypeId
                : CreateTypeId(constructor.DeclaringType),

            Parameters = constructor.Parameters.Select(p => MapConstructorParameter(declaringTypeId, p)).ToImmutableEquatableArray(),
            RequiredOrInitMembers = requiredOrInitMembers?.ToImmutableEquatableArray() ?? [],
            OptionalMembers = optionalProperties?.ToImmutableEquatableArray() ?? [],
            OptionalMemberFlagsType = (optionalProperties?.Count ?? 0) switch
            {
                0 => OptionalMemberFlagsType.None,
                <= 8 => OptionalMemberFlagsType.Byte,
                <= 16 => OptionalMemberFlagsType.UShort,
                <= 32 => OptionalMemberFlagsType.UInt32,
                <= 64 => OptionalMemberFlagsType.ULong,
                _ => OptionalMemberFlagsType.BitArray,
            },

            StaticFactoryName = constructor.Constructor.IsStatic ? constructor.Constructor.GetFullyQualifiedName() : null,
            IsPublic = constructor.Constructor.DeclaredAccessibility is Accessibility.Public,
        };
    }

    private static ConstructorParameterShapeModel MapConstructorParameter(TypeId declaringTypeId, ConstructorParameterDataModel parameter)
    {
        return new ConstructorParameterShapeModel
        {
            Name = parameter.Parameter.Name,
            Position = parameter.Parameter.Ordinal,
            DeclaringType = declaringTypeId,
            ParameterType = CreateTypeId(parameter.Parameter.Type),
            Kind = ParameterKind.ConstructorParameter,
            IsRequired = !parameter.HasDefaultValue,
            IsNonNullable = parameter.IsNonNullable,
            IsPublic = true,
            IsField = false,
            HasDefaultValue = parameter.HasDefaultValue,
            DefaultValueExpr = parameter.DefaultValueExpr,
        };
    }

    private static IEnumerable<ConstructorShapeModel> MapTupleConstructors(TypeId typeId, TupleDataModel tupleModel)
    {
        if (tupleModel.IsValueTuple)
        {
            // Return the default constructor for value tuples
            yield return new ConstructorShapeModel
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

        yield return new ConstructorShapeModel
        {
            DeclaringType = typeId,
            Parameters = tupleModel.Elements.Select((p, i) => MapTupleConstructorParameter(typeId, p, i)).ToImmutableEquatableArray(),
            RequiredOrInitMembers = [],
            OptionalMembers = [],
            OptionalMemberFlagsType = OptionalMemberFlagsType.None,
            StaticFactoryName = null,
            IsPublic = true,
        };

        static ConstructorParameterShapeModel MapTupleConstructorParameter(TypeId typeId, PropertyDataModel tupleElement, int position)
        {
            return new ConstructorParameterShapeModel
            {
                Name = $"Item{position + 1}",
                Position = position,
                ParameterType = CreateTypeId(tupleElement.PropertyType),
                DeclaringType = typeId,
                HasDefaultValue = false,
                Kind = ParameterKind.ConstructorParameter,
                IsRequired = true,
                IsPublic = true,
                IsField = true,
                IsNonNullable = tupleElement.IsSetterNonNullable,
                DefaultValueExpr = null,
            };
        }
    }
}