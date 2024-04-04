using Microsoft.CodeAnalysis;
using TypeShape.Roslyn;
using TypeShape.SourceGenerator.Helpers;
using TypeShape.SourceGenerator.Model;

namespace TypeShape.SourceGenerator;

public sealed partial class Parser
{
    private TypeShapeModel MapModel(TypeId typeId, TypeDataModel model, bool isGeneratedViaWitnessType)
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
                    .Select(c => MapConstructor(objectModel, typeId, c))
                    .ToImmutableEquatableArray(),

                Properties = objectModel.Properties
                    .Select(p => MapProperty(model.Type, typeId, p))
                    .OrderBy(p => p.Order)
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

            _ => new ObjectShapeModel
            { 
                Type = typeId,
                Constructors = [],
                Properties = [],
                IsValueTupleType = false,
                IsClassTupleType = false,
                IsRecord = false,
                EmitGenericTypeShapeProviderImplementation = emitGenericProviderImplementation,
            }
        };

        static bool IsFactoryAcceptingIEnumerable(IMethodSymbol? method)
        {
            return method?.Parameters is [{ Type: INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Collections_Generic_IEnumerable_T } }];
        }
    }

    private PropertyShapeModel MapProperty(ITypeSymbol parentType, TypeId parentTypeId, PropertyDataModel property, bool isClassTupleType = false, int tupleElementIndex = -1)
    {
        ParsePropertyShapeAttribute(property.PropertySymbol, out string propertyName, out int order);
        return new PropertyShapeModel
        {
            Name = isClassTupleType ? $"Item{tupleElementIndex + 1}" : propertyName ?? property.Name,
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
            Order = order,
        };
    }

    private ConstructorShapeModel MapConstructor(ObjectDataModel objectModel, TypeId declaringTypeId, ConstructorDataModel constructor)
    {
        int position = constructor.Parameters.Length;
        List<ConstructorParameterShapeModel>? requiredOrInitMembers = null;
        List<ConstructorParameterShapeModel>? optionalProperties = null;

        foreach (PropertyDataModel propertyModel in constructor.MemberInitializers.OrderByDescending(p => p.IsRequired || p.IsInitOnly))
        {
            ParsePropertyShapeAttribute(propertyModel.PropertySymbol, out string propertyName, out _);

            var memberInitializer = new ConstructorParameterShapeModel
            {
                ParameterType = CreateTypeId(propertyModel.PropertyType),
                DeclaringType = SymbolEqualityComparer.Default.Equals(propertyModel.DeclaringType, objectModel.Type)
                    ? declaringTypeId
                    : CreateTypeId(propertyModel.DeclaringType),

                Name = propertyName,
                UnderlyingMemberName = propertyModel.Name,
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
            DeclaringType = SymbolEqualityComparer.Default.Equals(constructor.DeclaringType, objectModel.Type)
                ? declaringTypeId
                : CreateTypeId(constructor.DeclaringType),

            Parameters = constructor.Parameters.Select(p => MapConstructorParameter(objectModel, declaringTypeId, p)).ToImmutableEquatableArray(),
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

    private ConstructorParameterShapeModel MapConstructorParameter(ObjectDataModel objectModel, TypeId declaringTypeId, ConstructorParameterDataModel parameter)
    {
        string name = parameter.Parameter.Name;

        AttributeData? parameterAttr = parameter.Parameter.GetAttribute(_knownSymbols.ParameterShapeAttribute);
        if (parameterAttr != null &&
            parameterAttr.TryGetNamedArgument("Name", out string? value) && value != null)
        {
            // Resolve the [ParameterShape] attribute name override
            name = value;
        }
        else
        {
            foreach (PropertyDataModel property in objectModel.Properties)
            {
                if (SymbolEqualityComparer.Default.Equals(property.PropertyType, parameter.Parameter.Type) &&
                    IsMatchingPropertyName(parameter.Parameter.Name, property.Name) &&
                    property.PropertySymbol.GetAttribute(_knownSymbols.PropertyShapeAttribute) is AttributeData attributeData &&
                    attributeData.TryGetNamedArgument("Name", out string? result) && result != null)
                {
                    // We have a matching property with a name override, use it in the parameter as well.
                    name = result;
                }

                // Match property names to parameters up to Pascal/camel case conversion.
                static bool IsMatchingPropertyName(string parameterName, string propertyName) =>
                    parameterName.Length == propertyName.Length &&
                    char.ToLowerInvariant(parameterName[0]) == char.ToLowerInvariant(propertyName[0]) &&
                    parameterName.AsSpan(start: 1).SequenceEqual(propertyName.AsSpan(start: 1));
            }
        }

        return new ConstructorParameterShapeModel
        {
            Name = name,
            UnderlyingMemberName = parameter.Parameter.Name,
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
            string name = $"Item{position + 1}";
            return new ConstructorParameterShapeModel
            {
                Name = name,
                UnderlyingMemberName = name,
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

    private void ParsePropertyShapeAttribute(ISymbol propertySymbol, out string propertyName, out int order)
    {
        propertyName = propertySymbol.Name;
        order = 0;

        if (propertySymbol.GetAttribute(_knownSymbols.PropertyShapeAttribute) is AttributeData propertyAttr)
        {
            foreach (KeyValuePair<string, TypedConstant> namedArgument in propertyAttr.NamedArguments)
            {
                switch (namedArgument.Key)
                {
                    case "Name":
                        propertyName = (string?)namedArgument.Value.Value ?? propertyName;
                        break;
                    case "Order":
                        order = (int)namedArgument.Value.Value!;
                        break;
                }
            }
        }
    }
}