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

        return type.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(ctor =>
                ctor is { MethodKind: MethodKind.Constructor, DeclaredAccessibility: Accessibility.Public } &&
                ctor.Parameters.All(p => IsSupportedType(p.Type)))
            .Where(ctor =>
                // For collection types only emit the default & copy constructors
                collectionInterface is null ||
                ctor.Parameters.Length == 0 ||
                ctor.Parameters.Length == 1 && SymbolEqualityComparer.Default.Equals(ctor.Parameters[0].Type, collectionInterface))
            .Select(ctor => MapConstructor(typeId, ctor))
            .ToImmutableArrayEq();
    }

    private ConstructorModel MapConstructor(TypeId typeId, IMethodSymbol constructor)
    {
        Debug.Assert(constructor.MethodKind is MethodKind.Constructor && constructor.DeclaredAccessibility is Accessibility.Public);

        var parameters = new List<ConstructorParameterModel>();
        foreach (IParameterSymbol param in constructor.Parameters)
        {
            parameters.Add(MapConstructorParameter(param));
        }

        string constructorArgumentStateFQN = parameters.Count switch
        {
            0 => "object?",
            1 => parameters[0].ParameterType.FullyQualifiedName,
            _ => $"({string.Join(", ", parameters.Select(p => p.ParameterType.FullyQualifiedName))})",
        };

        return new ConstructorModel
        {
            DeclaringType = typeId,
            ConstructorArgumentStateFQN = constructorArgumentStateFQN,
            Parameters = parameters.ToImmutableArrayEq(),
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
            HasDefaultValue = parameter.HasExplicitDefaultValue,
            DefaultValue = parameter.HasExplicitDefaultValue ? parameter.ExplicitDefaultValue : null,
            DefaultValueRequiresCast = parameter.Type 
                is { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } 
                or { TypeKind: TypeKind.Enum },
        };
    }
}
