using Microsoft.CodeAnalysis.Text;
using System.Diagnostics;
using PolyType.Roslyn;
using PolyType.SourceGenerator.Model;

namespace PolyType.SourceGenerator;

internal static partial class SourceFormatter
{
    private static SourceText FormatType(TypeShapeProviderModel provider, TypeShapeModel type)
    {
        string generatedPropertyType = $"global::PolyType.Abstractions.ITypeShape<{type.Type.FullyQualifiedName}>";
        string generatedFactoryMethodName = $"Create_{type.Type.GeneratedPropertyName}";
        string generatedFieldName = "_" + type.Type.GeneratedPropertyName;

        var writer = new SourceWriter();
        StartFormatSourceFile(writer, provider.Declaration);

        if (type.EmitGenericTypeShapeProviderImplementation)
        {
            writer.WriteLine("#nullable disable annotations // Use nullable-oblivious interface implementation", disableIndentation: true);
            writer.WriteLine($"""{provider.Declaration.TypeDeclarationHeader} : global::PolyType.IShapeable<{type.Type.FullyQualifiedName}>""");
            writer.WriteLine("#nullable enable annotations // Use nullable-oblivious interface implementation", disableIndentation: true);
        }
        else
        {
            writer.WriteLine(provider.Declaration.TypeDeclarationHeader);
        }

        writer.WriteLine('{');
        writer.Indentation++;

        writer.WriteLine("/// <summary>Gets the generated shape for specified type.</summary>");
        writer.WriteLine("#nullable disable annotations // Use nullable-oblivious property type", disableIndentation: true);
        writer.WriteLine($"public {generatedPropertyType} {type.Type.GeneratedPropertyName} => {generatedFieldName} ??= {generatedFactoryMethodName}();");
        writer.WriteLine("#nullable enable annotations // Use nullable-oblivious property type", disableIndentation: true);
        writer.WriteLine($"private {generatedPropertyType}? {generatedFieldName};");
        writer.WriteLine();

        if (type.EmitGenericTypeShapeProviderImplementation)
        {
            writer.WriteLine($"""
                static {generatedPropertyType} global::PolyType.IShapeable<{type.Type.FullyQualifiedName}>.GetShape() => Default.{type.Type.GeneratedPropertyName};

                """);
        }

        switch (type)
        {
            case ObjectShapeModel objectShapeModel:
                FormatObjectTypeShapeFactory(writer, generatedFactoryMethodName, objectShapeModel);
                break;

            case EnumShapeModel enumShapeModel:
                FormatEnumTypeShapeFactory(writer, generatedFactoryMethodName, enumShapeModel);
                break;

            case NullableShapeModel nullableShapeModel:
                FormatNullableTypeShapeFactory(writer, generatedFactoryMethodName, nullableShapeModel);
                break;

            case EnumerableShapeModel enumerableShapeModel:
                FormatEnumerableTypeShapeFactory(writer, generatedFactoryMethodName, enumerableShapeModel);
                break;

            case DictionaryShapeModel dictionaryShapeModel:
                FormatDictionaryTypeShapeFactory(writer, generatedFactoryMethodName, dictionaryShapeModel);
                break;

            default:
                Debug.Fail($"Should not be reached {type.GetType().Name}");
                throw new InvalidOperationException();
        }

        writer.Indentation--;
        writer.WriteLine('}');
        EndFormatSourceFile(writer);

        return writer.ToSourceText();
    }
}
