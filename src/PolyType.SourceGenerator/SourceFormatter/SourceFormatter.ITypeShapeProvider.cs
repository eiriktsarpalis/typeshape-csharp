using Microsoft.CodeAnalysis.Text;
using PolyType.Roslyn;
using PolyType.SourceGenerator.Model;

namespace PolyType.SourceGenerator;

internal sealed partial class SourceFormatter
{
    private static SourceText FormatProviderInterfaceImplementation(TypeShapeProviderModel provider)
    {
        var writer = new SourceWriter();
        StartFormatSourceFile(writer, provider.ProviderDeclaration);

        writer.WriteLine($"{provider.ProviderDeclaration.TypeDeclarationHeader} : global::PolyType.ITypeShapeProvider");
        writer.WriteLine('{');
        writer.Indentation++;

        writer.WriteLine("""
            /// <summary>
            /// Gets the generated <see cref="global::PolyType.Abstractions.ITypeShape" /> for the specified type.
            /// </summary>
            /// <param name="type">The type for which a shape is requested.</param>
            /// <returns>
            /// The generated <see cref="global::PolyType.Abstractions.ITypeShape" /> for the specified type.
            /// </returns>
            public global::PolyType.Abstractions.ITypeShape? GetShape(global::System.Type type)
            {
            """);

        writer.Indentation++;

        foreach (TypeShapeModel generatedType in provider.ProvidedTypes.Values)
        {
            writer.WriteLine($"""
                if (type == typeof({generatedType.Type.FullyQualifiedName}))
                    return {generatedType.SourceIdentifier};

                """);
        }

        writer.WriteLine("return null;");
        writer.Indentation--;
        writer.WriteLine('}');

        writer.Indentation--;
        writer.WriteLine('}');
        EndFormatSourceFile(writer);
        return writer.ToSourceText();
    }

    private SourceText FormatIShapeableOfTStub(TypeDeclarationModel typeDeclaration, TypeId typeToImplement, TypeShapeProviderModel provider)
    {
        var writer = new SourceWriter();
        StartFormatSourceFile(writer, typeDeclaration);

        writer.WriteLine("#nullable disable annotations // Use nullable-oblivious interface implementation", disableIndentation: true);
        writer.WriteLine($"{typeDeclaration.TypeDeclarationHeader} : global::PolyType.IShapeable<{typeToImplement.FullyQualifiedName}>");
        writer.WriteLine("#nullable enable annotations // Use nullable-oblivious interface implementation", disableIndentation: true);
        writer.WriteLine('{');
        writer.Indentation++;

        writer.WriteLine($"""
            static global::PolyType.Abstractions.ITypeShape<{typeToImplement.FullyQualifiedName}> global::PolyType.IShapeable<{typeToImplement.FullyQualifiedName}>.GetShape() 
                => {provider.ProviderDeclaration.Id.FullyQualifiedName}.{ProviderSingletonProperty}.{GetShapeModel(typeToImplement).SourceIdentifier};
            """);

        writer.Indentation--;
        writer.WriteLine('}');
        EndFormatSourceFile(writer);

        return writer.ToSourceText();
    }

    private static SourceText FormatITypeShapeProviderStub(TypeDeclarationModel typeDeclaration, TypeShapeProviderModel provider)
    {
        var writer = new SourceWriter();
        StartFormatSourceFile(writer, typeDeclaration);

        writer.WriteLine($$"""
            {{typeDeclaration.TypeDeclarationHeader}} : global::PolyType.ITypeShapeProvider
            {
                global::PolyType.Abstractions.ITypeShape? global::PolyType.ITypeShapeProvider.GetShape(global::System.Type type) 
                    => {{provider.ProviderDeclaration.Id.FullyQualifiedName}}.{{ProviderSingletonProperty}}.GetShape(type);
            }
            """);

        EndFormatSourceFile(writer);

        return writer.ToSourceText();
    }
}
