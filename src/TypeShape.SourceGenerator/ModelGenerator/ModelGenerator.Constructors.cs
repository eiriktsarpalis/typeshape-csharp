using Microsoft.CodeAnalysis;
using System.Diagnostics;
using TypeShape.SourceGenerator.Helpers;
using TypeShape.SourceGenerator.Model;

namespace TypeShape.SourceGenerator;

public sealed partial class ModelGenerator
{
    private ImmutableArrayEq<ConstructorModel> MapConstructors(TypeId typeId, ITypeSymbol type, ITypeSymbol[]? classTupleElements, ITypeSymbol? collectionInterface, bool disallowMemberResolution)
    {
        if (TryResolveFactoryMethod(type) is { } factoryMethod)
        {
            return ImmutableArrayEq.Create(MapConstructor(typeId, factoryMethod));
        }

        if (disallowMemberResolution || type.TypeKind is not (TypeKind.Struct or TypeKind.Class) || type.SpecialType is not SpecialType.None)
        {
            return ImmutableArrayEq<ConstructorModel>.Empty;
        }
        
        if (classTupleElements is not null)
        {
            return MapTupleConstructors(typeId, type, classTupleElements).ToImmutableArrayEq();
        }

        if (type is INamedTypeSymbol namedType && namedType.IsTupleType)
        {
            return MapTupleConstructors(typeId, namedType, namedType.TupleElements.Select(e => e.Type)).ToImmutableArrayEq();
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

    private ConstructorModel MapConstructor(TypeId typeId, IMethodSymbol constructor, ConstructorParameterModel[]? requiredOrInitOnlyMembers = null)
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

        foreach (ConstructorParameterModel memberInitializer in (requiredOrInitOnlyMembers ?? Array.Empty<ConstructorParameterModel>()))
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
            StaticFactoryName = constructor.IsStatic 
            ? $"{constructor.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}.{constructor.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}" 
            : null,
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

        TypeId typeId = EnqueueForGeneration(fieldSymbol.Type);
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

    private IEnumerable<ConstructorModel> MapTupleConstructors(TypeId typeId, ITypeSymbol tupleType, IEnumerable<ITypeSymbol> tupleElements)
    {
        if (tupleType.IsValueType)
        {
            // Return the default constructor for value tuples
            yield return new ConstructorModel
            {
                DeclaringType = typeId,
                Parameters = ImmutableArrayEq<ConstructorParameterModel>.Empty,
                MemberInitializers = ImmutableArrayEq<ConstructorParameterModel>.Empty,
                StaticFactoryName = null,
            };
        }

        yield return new ConstructorModel
        {
            DeclaringType = typeId,
            Parameters = tupleElements.Select(MapTupleConstructorParameter).ToImmutableArrayEq(),
            MemberInitializers = ImmutableArrayEq<ConstructorParameterModel>.Empty,
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
                IsAutoProperty = false,
                DefaultValue = null,
                DefaultValueRequiresCast = false,
            };
        }
    }

    private IMethodSymbol? TryResolveFactoryMethod(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol arrayType && arrayType.Rank == 1)
        {
            return _semanticModel.Compilation.GetTypeByMetadataName("System.Linq.Enumerable")
                .GetMethodSymbol(method =>
                    method.IsStatic && method.IsGenericMethod && method.Name is "ToArray" &&
                    method.Parameters.Length == 1 && method.Parameters[0].Type.Name == "IEnumerable")
                .MakeGenericMethod(arrayType.ElementType);
        }

        if (type is not INamedTypeSymbol namedType || !namedType.IsGenericType)
        {
            return null;
        }

        SymbolEqualityComparer cmp = SymbolEqualityComparer.Default;

        if (cmp.Equals(namedType.ConstructedFrom, _immutableArray))
        { 
            return _semanticModel.Compilation.GetTypeByMetadataName("System.Collections.Immutable.ImmutableArray")
                .GetMethodSymbol(method =>
                    method.IsStatic && method.IsGenericMethod && method.Name is "CreateRange" &&
                    method.Parameters.Length == 1 && method.Parameters[0].Type.Name == "IEnumerable")
                .MakeGenericMethod(namedType.TypeArguments[0]);
        }

        if (cmp.Equals(namedType.ConstructedFrom, _immutableList))
        {
            return _semanticModel.Compilation.GetTypeByMetadataName("System.Collections.Immutable.ImmutableList")
                .GetMethodSymbol(method =>
                    method.IsStatic && method.IsGenericMethod && method.Name is "CreateRange" &&
                    method.Parameters.Length == 1 && method.Parameters[0].Type.Name == "IEnumerable")
                .MakeGenericMethod(namedType.TypeArguments[0]);
        }

        if (cmp.Equals(namedType.ConstructedFrom, _immutableDictionary))
        {
            return _semanticModel.Compilation.GetTypeByMetadataName("System.Collections.Immutable.ImmutableDictionary")
                .GetMethodSymbol(method =>
                    method.IsStatic && method.IsGenericMethod && method.Name is "CreateRange" &&
                    method.Parameters.Length == 1 && method.Parameters[0].Type.Name == "IEnumerable")
                .MakeGenericMethod(namedType.TypeArguments[0], namedType.TypeArguments[1]);
        }

        return null;
    }
}
