using TypeShape.Roslyn;
using TypeShape.SourceGenerator.Model;

namespace TypeShape.SourceGenerator;

internal static partial class SourceFormatter
{
    private static void FormatEnumTypeShapeFactory(SourceWriter writer, string methodName, EnumShapeModel enumShapeType)
    {
        writer.WriteLine($$"""
            private ITypeShape<{{enumShapeType.Type.FullyQualifiedName}}> {{methodName}}()
            {
                return new SourceGenEnumTypeShape<{{enumShapeType.Type.FullyQualifiedName}}, {{enumShapeType.UnderlyingType.FullyQualifiedName}}>
                {
                    Provider = this,
                    UnderlyingType = {{enumShapeType.UnderlyingType.GeneratedPropertyName}},
                };
            }
            """);
    }
}
