using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using TypeShape.Roslyn.Helpers;

namespace TypeShape.Roslyn;

public partial class TypeDataModelGenerator
{
    /// <summary>
    /// Whether property or field resolution should be skipped for the given type.
    /// </summary>
    /// <remarks>
    /// Currently skipped for simple types, nullable types, and <see cref="MemberInfo"/>.
    /// </remarks>
    protected virtual bool SkipObjectMemberResolution(INamedTypeSymbol type)
    {
        return
            type.TypeKind is not (TypeKind.Class or TypeKind.Struct or TypeKind.Interface) ||
            KnownSymbols.Compilation.IsSimpleType(KnownSymbols.CoreLibAssembly, type) ||
            type.SpecialType is SpecialType.System_Nullable_T ||
            KnownSymbols.MemberInfoType.IsAssignableFrom(type);
    }

    /// <summary>
    /// Resolves the constructor symbols that should be included for the given type.
    /// </summary>
    protected virtual IEnumerable<IMethodSymbol> ResolveConstructors(ITypeSymbol type)
    {
        IMethodSymbol[] foundConstructors = type.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(ctor =>
                ctor is { IsStatic: false, MethodKind: MethodKind.Constructor } &&
                ctor.Parameters.All(p => IsSupportedType(p.Type)) &&
                IsAccessibleSymbol(ctor))
            // Skip the copy constructor for record types
            .Where(ctor => !(type.IsRecord && ctor.Parameters.Length == 1 && SymbolEqualityComparer.Default.Equals(ctor.Parameters[0].Type, type)))
            .ToArray();

        return foundConstructors
            // Only include the implicit constructor in structs if there are no other constructors
            .Where(ctor => !ctor.IsImplicitlyDeclared || foundConstructors.Length == 1);
    }

    /// <summary>
    /// Whether the given property should be ignored from the data model.
    /// </summary>
    /// <remarks>Defaults to non-public properties being skipped.</remarks>
    protected virtual bool IgnoreProperty(IPropertySymbol propertySymbol) => propertySymbol.DeclaredAccessibility is not Accessibility.Public;

    /// <summary>
    /// Whether the given field should be ignored from the data model.
    /// </summary>
    /// <remarks>Defaults to non-public fields being skipped.</remarks>
    protected virtual bool IgnoreField(IFieldSymbol fieldSymbol) => fieldSymbol.DeclaredAccessibility is not Accessibility.Public;

    private bool TryMapObject(ITypeSymbol type, ref TypeDataModelGenerationContext ctx, out TypeDataModel? model, out TypeDataModelGenerationStatus status)
    {
        status = default;
        model = null;

        if (type is not INamedTypeSymbol namedType ||
            type.TypeKind is not (TypeKind.Struct or TypeKind.Class or TypeKind.Interface) ||
            SkipObjectMemberResolution(namedType))
        {
            // Objects must be named classes, structs, or interfaces.
            return false;
        }

        ImmutableArray<PropertyDataModel> properties = MapProperties(namedType, ref ctx);
        ImmutableArray<ConstructorDataModel> constructors = MapConstructors(namedType, properties, ref ctx);

        model = new ObjectDataModel
        {
            Type = type,
            Constructors = constructors,
            Properties = properties,
        };

        return true;
    }

    private ImmutableArray<PropertyDataModel> MapProperties(INamedTypeSymbol type, ref TypeDataModelGenerationContext ctx)
    {
        List<PropertyDataModel> properties = [];
        HashSet<string> membersInScope = new(StringComparer.Ordinal);

        foreach (ITypeSymbol current in type.GetSortedTypeHierarchy())
        {
            IOrderedEnumerable<ISymbol> members = current.GetMembers()
                .Where(m => m.Kind is SymbolKind.Field or SymbolKind.Property)
                .OrderByDescending(m => m.Kind is SymbolKind.Property); // for consistency with reflection, sort properties ahead of fields
            
            foreach (ISymbol member in members) 
            {
                if (member is IPropertySymbol { IsStatic: false, Parameters: [] } ps &&
                    IsAccessibleSymbol(ps) && !IsOverriddenOrShadowed(ps) && !IgnoreProperty(ps) && 
                    IncludeNestedType(ps.Type, ref ctx) is TypeDataModelGenerationStatus.Success)
                {
                    PropertyDataModel propertyModel = MapProperty(ps);
                    properties.Add(propertyModel);
                }
                else if (
                    member is IFieldSymbol { IsStatic: false, IsConst: false } fs &&
                    IsAccessibleSymbol(fs) && !IsOverriddenOrShadowed(fs) && !IgnoreField(fs) && 
                    IncludeNestedType(fs.Type, ref ctx) is TypeDataModelGenerationStatus.Success)
                {
                    PropertyDataModel fieldModel = MapField(fs);
                    properties.Add(fieldModel);
                }

                bool IsOverriddenOrShadowed(ISymbol member) => member.IsOverride || !membersInScope.Add(member.Name);
            }
        }

        return properties.ToImmutableArray();
    }

    private PropertyDataModel MapProperty(IPropertySymbol property)
    {
        Debug.Assert(property is { IsStatic: false, IsIndexer: false });
        property.ResolveNullableAnnotation(out bool isGetterNonNullable, out bool isSetterNonNullable);
        return new PropertyDataModel(property)
        {
            CanRead = property.GetMethod is { } getter && IsAccessibleSymbol(getter),
            CanWrite = property.SetMethod is IMethodSymbol { IsInitOnly: false } setter && IsAccessibleSymbol(setter),
            IsGetterNonNullable = isGetterNonNullable,
            IsSetterNonNullable = isSetterNonNullable,
        };
    }

    private static PropertyDataModel MapField(IFieldSymbol field)
    {
        Debug.Assert(!field.IsStatic);
        field.ResolveNullableAnnotation(out bool isGetterNonNullable, out bool isSetterNonNullable);
        return new PropertyDataModel(field)
        {
            CanRead = true,
            CanWrite = !field.IsReadOnly,
            IsGetterNonNullable = isGetterNonNullable,
            IsSetterNonNullable = isSetterNonNullable,
        };
    }

    private ImmutableArray<ConstructorDataModel> MapConstructors(INamedTypeSymbol type, ImmutableArray<PropertyDataModel> properties, ref TypeDataModelGenerationContext ctx)
    {
        List<ConstructorDataModel> results = [];
        foreach (IMethodSymbol constructor in ResolveConstructors(type))
        {
            ConstructorDataModel? constructorModel = MapConstructor(type, constructor, properties, ref ctx);
            if (constructorModel is not null)
            {
                results.Add(constructorModel.Value);
            }
        }

        return results.ToImmutableArray();
    }

    private ConstructorDataModel? MapConstructor(ITypeSymbol type, IMethodSymbol constructor, ImmutableArray<PropertyDataModel> properties, ref TypeDataModelGenerationContext ctx)
    {
        Debug.Assert(constructor.MethodKind is MethodKind.Constructor || constructor.IsStatic);
        Debug.Assert(IsAccessibleSymbol(constructor));

        var parameters = new List<ConstructorParameterDataModel>();
        TypeDataModelGenerationContext scopedCtx = ctx;
        foreach (IParameterSymbol parameter in constructor.Parameters)
        {
            if (IncludeNestedType(parameter.Type, ref scopedCtx) != TypeDataModelGenerationStatus.Success)
            {
                // Skip constructors with unsupported parameter types
                return null;
            }

            ConstructorParameterDataModel parameterModel = MapConstructorParameter(parameter);
            parameters.Add(parameterModel);
        }

        ctx = scopedCtx; // Commit constructor parameter resolution to parent context
        bool setsRequiredMembers = constructor.HasSetsRequiredMembersAttribute();
        Dictionary<string, IParameterSymbol>? parameterIndex = null;
        List<PropertyDataModel>? memberInitializers = null;

        foreach (PropertyDataModel property in properties)
        {
            if (!property.CanWrite && !property.IsInitOnly)
            {
                // We're only interested in settable properties.
                continue;
            }

            if (setsRequiredMembers && property.IsRequired)
            {
                // Skip required members if set by the constructor.
                continue;
            }

            if (!property.IsRequired && property.PropertySymbol.IsAutoProperty() && MatchesConstructorParameter(property))
            {
                // Deduplicate any auto properties whose signature matches a constructor parameter.
                continue;
            }

            (memberInitializers ??= []).Add(property);

            bool MatchesConstructorParameter(PropertyDataModel settableProperty)
            {
                parameterIndex ??= constructor.Parameters.ToDictionary(p => p.Name, p => p, StringComparer.Ordinal);
                return parameterIndex.TryGetValue(settableProperty.Name, out IParameterSymbol? matchingParameter) &&
                    SymbolEqualityComparer.Default.Equals(settableProperty.PropertyType, matchingParameter.Type);
            }
        }

        return new ConstructorDataModel
        {
            Constructor = constructor,
            Parameters = parameters.ToImmutableArray(),
            MemberInitializers = memberInitializers?.ToImmutableArray() ?? ImmutableArray<PropertyDataModel>.Empty,
        };
    }

    private static ConstructorParameterDataModel MapConstructorParameter(IParameterSymbol parameter)
    {
        return new ConstructorParameterDataModel
        {
            Parameter = parameter
        };
    }
}
