using System.Diagnostics;
using Microsoft.CodeAnalysis;

namespace TypeShape.Roslyn;

public partial class TypeDataModelGenerator
{
    private bool TryMapEnum(ITypeSymbol type, ref TypeDataModelGenerationContext ctx, out TypeDataModel? model, out TypeDataModelGenerationStatus status)
    {
        if (type.TypeKind is not TypeKind.Enum)
        {
            model = null;
            status = default;
            return false;
        }

        INamedTypeSymbol underlyingType = ((INamedTypeSymbol)type).EnumUnderlyingType!;
        status = IncludeNestedType(underlyingType, ref ctx);
        Debug.Assert(status is TypeDataModelGenerationStatus.Success);

        model = new EnumDataModel
        {
            Type = type,
            UnderlyingType = underlyingType,
        };
        
        return true;
    }
}
