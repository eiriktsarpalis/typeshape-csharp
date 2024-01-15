using Microsoft.CodeAnalysis.Text;
using TypeShape.Roslyn;
using TypeShape.SourceGenerator.Model;

namespace TypeShape.SourceGenerator;

internal static partial class SourceFormatter
{
    private static SourceText FormatType(TypeShapeProviderModel provider, TypeShapeModel type)
    {
        string generatedPropertyType = $"ITypeShape<{type.Type.FullyQualifiedName}>";
        string generatedFactoryMethodName = $"Create_{type.Type.GeneratedPropertyName}";
        string generatedFieldName = "_" + type.Type.GeneratedPropertyName;
        string? propertiesFactoryMethodName = type is ObjectShapeModel ? $"CreateProperties_{type.Type.GeneratedPropertyName}" : null;
        string? constructorFactoryMethodName = type is ObjectShapeModel ? $"CreateConstructors_{type.Type.GeneratedPropertyName}" : null;
        string? enumFactoryMethodName = type is EnumShapeModel ? $"CreateEnumType_{type.Type.GeneratedPropertyName}" : null;
        string? nullableFactoryMethodName = type is NullableShapeModel ? $"CreateNullableType_{type.Type.GeneratedPropertyName}" : null;
        string? dictionaryFactoryMethodName = type is DictionaryShapeModel ? $"CreateDictionaryType_{type.Type.GeneratedPropertyName}" : null;
        string? enumerableFactoryMethodName = type is EnumerableShapeModel ? $"CreateEnumerableType_{type.Type.GeneratedPropertyName}" : null;

        var writer = new SourceWriter();
        StartFormatSourceFile(writer, provider.Declaration);

        if (type.EmitGenericTypeShapeProviderImplementation)
        {
            writer.WriteLine($"""
                #nullable disable annotations // Use nullable-oblivious interface implementation
                {provider.Declaration.TypeDeclarationHeader} : ITypeShapeProvider<{type.Type.FullyQualifiedName}>
                #nullable enable annotations // Use nullable-oblivious interface implementation
                """);
        }
        else
        {
            writer.WriteLine(provider.Declaration.TypeDeclarationHeader);
        }

        writer.WriteLine('{');
        writer.Indentation++;

        writer.WriteLine($"""
            #nullable disable annotations // Use nullable-oblivious property type
            public {generatedPropertyType} {type.Type.GeneratedPropertyName} => {generatedFieldName} ??= {generatedFactoryMethodName}();
            #nullable enable annotations // Use nullable-oblivious property type

            private {generatedPropertyType}? {generatedFieldName};

            """);

        if (type.EmitGenericTypeShapeProviderImplementation)
        {
            writer.WriteLine($"""
                static {generatedPropertyType} ITypeShapeProvider<{type.Type.FullyQualifiedName}>.GetShape() => Default.{type.Type.GeneratedPropertyName};

                """);
        }

        writer.WriteLine($$"""
            private {{generatedPropertyType}} {{generatedFactoryMethodName}}()
            {
                return new SourceGenTypeShape<{{type.Type.FullyQualifiedName}}>
                {
                    Provider = this,
                    AttributeProvider = typeof({{type.Type.FullyQualifiedName}}),
                    CreatePropertiesFunc = {{FormatNull(propertiesFactoryMethodName)}},
                    CreateConstructorsFunc = {{FormatNull(constructorFactoryMethodName)}},
                    CreateEnumShapeFunc = {{FormatNull(enumFactoryMethodName)}},
                    CreateNullableShapeFunc = {{FormatNull(nullableFactoryMethodName)}},
                    CreateDictionaryShapeFunc = {{FormatNull(dictionaryFactoryMethodName)}},
                    CreateEnumerableShapeFunc = {{FormatNull(enumerableFactoryMethodName)}},
                    IsRecord = {{FormatBool(type is ObjectShapeModel { IsRecord: true })}},
                };
            }
            """, trimNullAssignmentLines: true);

        if (propertiesFactoryMethodName != null)
        {
            writer.WriteLine();
            FormatPropertyFactory(writer, propertiesFactoryMethodName, (ObjectShapeModel)type);
        }

        if (constructorFactoryMethodName != null)
        {
            writer.WriteLine();
            FormatConstructorFactory(writer, constructorFactoryMethodName, (ObjectShapeModel)type);
        }

        if (enumFactoryMethodName != null)
        {
            writer.WriteLine();
            FormatEnumTypeFactory(writer, enumFactoryMethodName, (EnumShapeModel)type);
        }

        if (nullableFactoryMethodName != null)
        {
            writer.WriteLine();
            FormatNullableTypeFactory(writer, nullableFactoryMethodName, (NullableShapeModel)type);
        }

        if (enumerableFactoryMethodName != null)
        {
            writer.WriteLine();
            FormatEnumerableTypeFactory(writer, enumerableFactoryMethodName, (EnumerableShapeModel)type);
        }

        if (dictionaryFactoryMethodName != null)
        {
            writer.WriteLine();
            FormatDictionaryTypeFactory(writer, dictionaryFactoryMethodName, (DictionaryShapeModel)type);
        }

        writer.Indentation--;
        writer.WriteLine('}');
        EndFormatSourceFile(writer);

        return writer.ToSourceText();
    }
}
