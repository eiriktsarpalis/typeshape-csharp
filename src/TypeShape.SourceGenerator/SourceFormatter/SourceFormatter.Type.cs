using Microsoft.CodeAnalysis.Text;
using TypeShape.SourceGenerator.Helpers;
using TypeShape.SourceGenerator.Model;

namespace TypeShape.SourceGenerator;

internal static partial class SourceFormatter
{
    private static SourceText FormatType(TypeShapeProviderModel provider, TypeModel type)
    {
        string generatedPropertyType = $"ITypeShape<{type.Id.FullyQualifiedName}>";
        string generatedFactoryMethodName = $"Create_{type.Id.GeneratedPropertyName}";
        string generatedFieldName = "_" + type.Id.GeneratedPropertyName;
        string? propertiesFactoryMethodName = type.Properties != null ? $"CreateProperties_{type.Id.GeneratedPropertyName}" : null;
        string? constructorFactoryMethodName = type.Constructors != null ? $"CreateConstructors_{type.Id.GeneratedPropertyName}" : null;
        string? enumFactoryMethodName = type.EnumType is not null ? $"CreateEnumType_{type.Id.GeneratedPropertyName}" : null;
        string? nullableFactoryMethodName = type.NullableType is not null ? $"CreateNullableType_{type.Id.GeneratedPropertyName}" : null;
        string? dictionaryFactoryMethodName = type.DictionaryType is not null ? $"CreateDictionaryType_{type.Id.GeneratedPropertyName}" : null;
        string? enumerableFactoryMethodName = type.EnumerableType is not null ? $"CreateEnumerableType_{type.Id.GeneratedPropertyName}" : null;

        var writer = new SourceWriter();
        StartFormatSourceFile(writer, provider.Declaration);

        if (type.EmitGenericTypeShapeProviderImplementation)
        {
            writer.WriteLine($"""
                #nullable disable annotations // Use nullable-oblivious interface implementation
                {provider.Declaration.TypeDeclarationHeader} : ITypeShapeProvider<{type.Id.FullyQualifiedName}>
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
            public {generatedPropertyType} {type.Id.GeneratedPropertyName} => {generatedFieldName} ??= {generatedFactoryMethodName}();
            #nullable enable annotations // Use nullable-oblivious property type

            private {generatedPropertyType}? {generatedFieldName};

            """);

        if (type.EmitGenericTypeShapeProviderImplementation)
        {
            writer.WriteLine($"""
                static {generatedPropertyType} ITypeShapeProvider<{type.Id.FullyQualifiedName}>.GetShape() => Default.{type.Id.GeneratedPropertyName};

                """);
        }

        writer.WriteLine($$"""
            private {{generatedPropertyType}} {{generatedFactoryMethodName}}()
            {
                return new SourceGenTypeShape<{{type.Id.FullyQualifiedName}}>
                {
                    Provider = this,
                    AttributeProvider = typeof({{type.Id.FullyQualifiedName}}),
                    CreatePropertiesFunc = {{FormatNull(propertiesFactoryMethodName)}},
                    CreateConstructorsFunc = {{FormatNull(constructorFactoryMethodName)}},
                    CreateEnumShapeFunc = {{FormatNull(enumFactoryMethodName)}},
                    CreateNullableShapeFunc = {{FormatNull(nullableFactoryMethodName)}},
                    CreateDictionaryShapeFunc = {{FormatNull(dictionaryFactoryMethodName)}},
                    CreateEnumerableShapeFunc = {{FormatNull(enumerableFactoryMethodName)}},
                    IsRecord = {{FormatBool(type.IsRecord)}},
                };
            }
            """, trimNullAssignmentLines: true);

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

        writer.Indentation--;
        writer.WriteLine('}');
        EndFormatSourceFile(writer);

        return writer.ToSourceText();
    }
}
