using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using TypeShape.Roslyn.Helpers;

namespace TypeShape.Roslyn;

public partial class TypeDataModelGenerator
{
    /// <summary>
    /// Determines whether <see cref="System.Tuple" /> tuples should be mapped to 
    /// <see cref="TupleDataModel"/> instances and flattened to a single type for
    /// tuples containing over 8 elements.
    /// </summary>
    /// <remarks>
    /// Defaults to false, since C# treats
    /// <see cref="System.Tuple"/> types as regular classes. Set to true if 
    /// there is a need to handle F# models that do have syntactic sugar support
    /// for <see cref="System.Tuple"/>.
    /// </remarks>
    protected virtual bool FlattenSystemTupleTypes => false;

    private bool TryMapTuple(ITypeSymbol type, ref TypeDataModelGenerationContext ctx, out TypeDataModel? model, out TypeDataModelGenerationStatus status)
    {
        status = default;
        model = null;

        if (type is not INamedTypeSymbol namedType)
        {
            // Objects must be named classes, structs, or interfaces.
            return false;
        }

        if (namedType.IsTupleType && namedType.TupleElements.Length > 1) // Only tuples of arity > 1 have syntactic sugar support in C#.
        {
            var elements = new List<PropertyDataModel>();
            foreach (IFieldSymbol element in namedType.TupleElements)
            {
                if ((status = IncludeNestedType(element.Type, ref ctx)) != TypeDataModelGenerationStatus.Success)
                {
                    // Return true to indicate that the type is an unsupported tuple type.
                    return true;
                }

                PropertyDataModel propertyModel = MapField(element);
                elements.Add(propertyModel);
            }

            model = new TupleDataModel
            {
                Type = type,
                Elements = elements.ToImmutableArray(),
                IsValueTuple = true,
            };

            status = TypeDataModelGenerationStatus.Success;
            return true;
        }

        if (FlattenSystemTupleTypes && 
            KnownSymbols.Compilation.GetClassTupleProperties(KnownSymbols.CoreLibAssembly, namedType) 
            is IPropertySymbol[] classTupleProperties)
        {
            var elements = new List<PropertyDataModel>();
            foreach (IPropertySymbol elementProp in classTupleProperties)
            {
                if ((status = IncludeNestedType(elementProp.Type, ref ctx)) != TypeDataModelGenerationStatus.Success)
                {
                    // Return true to indicate that the type is an unsupported tuple type.
                    return true;
                }

                PropertyDataModel propertyModel = MapProperty(type, elementProp);
                elements.Add(propertyModel);
            }

            model = new TupleDataModel
            {
                Type = type,
                Elements = elements.ToImmutableArray(),
                IsValueTuple = false,
            };

            status = TypeDataModelGenerationStatus.Success;
            return true;
        }

        return false;
    }
}
