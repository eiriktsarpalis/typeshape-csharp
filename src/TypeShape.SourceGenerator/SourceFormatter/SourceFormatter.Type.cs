using Microsoft.CodeAnalysis.Text;
using TypeShape.SourceGenerator.Helpers;
using TypeShape.SourceGenerator.Model;

namespace TypeShape.SourceGenerator;

internal static partial class SourceFormatter
{
    private static SourceText FormatType(TypeShapeProviderModel provider, TypeModel type)
    {
        string generatedPropertyType = $"global::TypeShape.IType<{type.Id.FullyQualifiedName}>";
        string generatedFactoryMethodName = $"Create_{type.Id.GeneratedPropertyName}";
        string generatedFieldName = "_" + type.Id.GeneratedPropertyName;
        string? propertiesFactoryMethodName = type.Properties?.Count > 0 ? $"CreateProperties_{type.Id.GeneratedPropertyName}" : null;
        string? constructorFactoryMethodName = type.Constructors?.Count > 0 ? $"CreateConstructors_{type.Id.GeneratedPropertyName}" : null;
        string? enumFactoryMethodName = type.EnumType is not null ? $"CreateEnumType_{type.Id.GeneratedPropertyName}" : null;
        string? nullableFactoryMethodName = type.NullableType is not null ? $"CreateNullableType_{type.Id.GeneratedPropertyName}" : null;
        string? dictionaryFactoryMethodName = type.DictionaryType is not null ? $"CreateDictionaryType_{type.Id.GeneratedPropertyName}" : null;
        string? enumerableFactoryMethodName = type.EnumerableType is not null ? $"CreateEnumerableType_{type.Id.GeneratedPropertyName}" : null;

        var writer = new SourceWriter();
        StartFormatSourceFile(writer, provider);

        writer.WriteLine(provider.TypeDeclaration);
        writer.WriteStartBlock();

        writer.WriteLine($"""
            public {generatedPropertyType} {type.Id.GeneratedPropertyName} => {generatedFieldName} ??= {generatedFactoryMethodName}();
            private {generatedPropertyType}? {generatedFieldName};

            """);

        writer.WriteLine($$"""
            private {{generatedPropertyType}} {{generatedFactoryMethodName}}()
            {
                return new global::TypeShape.SourceGenModel.SourceGenType<{{type.Id.FullyQualifiedName}}>
                {
                    Provider = this,
                    AttributeProvider = typeof({{type.Id.FullyQualifiedName}}),
                    CreatePropertiesFunc = {{FormatNull(propertiesFactoryMethodName)}},
                    CreateConstructorsFunc = {{FormatNull(constructorFactoryMethodName)}},
                    CreateEnumTypeFunc = {{FormatNull(enumFactoryMethodName)}},
                    CreateNullableTypeFunc = {{FormatNull(nullableFactoryMethodName)}},
                    CreateDictionaryTypeFunc = {{FormatNull(dictionaryFactoryMethodName)}},
                    CreateEnumerableTypeFunc = {{FormatNull(enumerableFactoryMethodName)}},
                };
            }
            """);

        if (propertiesFactoryMethodName != null)
        {
            writer.WriteLine();
            FormatPropertyFactory(writer, propertiesFactoryMethodName, type);
        }

        if (constructorFactoryMethodName != null)
        {
            writer.WriteLine();
            FormatConstructorFactory(writer, constructorFactoryMethodName, type);
        }

        if (enumFactoryMethodName != null)
        {
            writer.WriteLine();
            FormatEnumTypeFactory(writer, enumFactoryMethodName, type.EnumType!);
        }

        if (nullableFactoryMethodName != null)
        {
            writer.WriteLine();
            FormatNullableTypeFactory(writer, nullableFactoryMethodName, type.NullableType!);
        }

        if (enumerableFactoryMethodName != null)
        {
            writer.WriteLine();
            FormatEnumerableTypeFactory(writer, enumerableFactoryMethodName, type.EnumerableType!);
        }

        if (dictionaryFactoryMethodName != null)
        {
            writer.WriteLine();
            FormatDictionaryTypeFactory(writer, dictionaryFactoryMethodName, type.DictionaryType!);
        }

        writer.WriteEndBlock();
        EndFormatSourceFile(writer);

        return writer.ToSourceText();
    }
}
