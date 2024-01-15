using Microsoft.CodeAnalysis;

namespace TypeShape.Roslyn;

public partial class TypeDataModelGenerator
{
    private static bool TryMapEnum(ITypeSymbol type, out TypeDataModel? model, out TypeDataModelGenerationStatus status)
    {
        if (type.TypeKind is not TypeKind.Enum)
        {
            model = null;
            status = default;
            return false;
        }

        model = new EnumDataModel
        {
            Type = type,
            UnderlyingType = ((INamedTypeSymbol)type).EnumUnderlyingType!,
        };

        status = TypeDataModelGenerationStatus.Success;
        return true;
    }
}
