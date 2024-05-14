using TypeShape.Roslyn;
using TypeShape.SourceGenerator.Model;

namespace TypeShape.SourceGenerator;

internal static partial class SourceFormatter
{
    private static void FormatEnumTypeShapeFactory(SourceWriter writer, string methodName, EnumShapeModel enumShapeType)
    {
        writer.WriteLine($$"""
            private global::TypeShape.Abstractions.ITypeShape<{{enumShapeType.Type.FullyQualifiedName}}> {{methodName}}()
            {
                return new global::TypeShape.SourceGenModel.SourceGenEnumTypeShape<{{enumShapeType.Type.FullyQualifiedName}}, {{enumShapeType.UnderlyingType.FullyQualifiedName}}>
                {
                    UnderlyingType = {{enumShapeType.UnderlyingType.GeneratedPropertyName}},
                    Provider = this,
                };
            }
            """);
    }
}
