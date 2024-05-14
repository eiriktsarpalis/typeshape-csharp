using TypeShape.Roslyn;
using TypeShape.SourceGenerator.Model;

namespace TypeShape.SourceGenerator;

internal static partial class SourceFormatter
{
    private static void FormatNullableTypeShapeFactory(SourceWriter writer, string methodName, NullableShapeModel nullableShapeModel)
    {
        writer.WriteLine($$"""
            private global::TypeShape.Abstractions.ITypeShape<{{nullableShapeModel.Type.FullyQualifiedName}}> {{methodName}}()
            {
                return new global::TypeShape.SourceGenModel.SourceGenNullableTypeShape<{{nullableShapeModel.ElementType.FullyQualifiedName}}>
                {
                    Provider = this,
                    ElementType = {{nullableShapeModel.ElementType.GeneratedPropertyName}},
                };
            }
            """);
    }
}
