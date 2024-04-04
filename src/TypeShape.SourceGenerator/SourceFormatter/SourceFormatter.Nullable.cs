using TypeShape.Roslyn;
using TypeShape.SourceGenerator.Model;

namespace TypeShape.SourceGenerator;

internal static partial class SourceFormatter
{
    private static void FormatNullableTypeShapeFactory(SourceWriter writer, string methodName, NullableShapeModel nullableShapeModel)
    {
        writer.WriteLine($$"""
            private ITypeShape<{{nullableShapeModel.Type.FullyQualifiedName}}> {{methodName}}()
            {
                return new SourceGenNullableTypeShape<{{nullableShapeModel.ElementType.FullyQualifiedName}}>
                {
                    Provider = this,
                    ElementType = {{nullableShapeModel.ElementType.GeneratedPropertyName}},
                };
            }
            """);
    }
}
