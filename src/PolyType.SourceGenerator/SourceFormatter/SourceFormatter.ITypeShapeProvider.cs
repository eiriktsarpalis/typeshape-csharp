using Microsoft.CodeAnalysis.Text;
using PolyType.Roslyn;
using PolyType.SourceGenerator.Model;

namespace PolyType.SourceGenerator;

internal static partial class SourceFormatter
{
    private static SourceText FormatProviderInterfaceImplementation(TypeShapeProviderModel provider)
    {
        var writer = new SourceWriter();
        StartFormatSourceFile(writer, provider.Declaration);

        writer.WriteLine($"{provider.Declaration.TypeDeclarationHeader} : global::PolyType.ITypeShapeProvider");
        writer.WriteLine('{');
        writer.Indentation++;

        writer.WriteLine("""
            /// <summary>
            /// Gets the generated <see cref="global::PolyType.Abstractions.ITypeShape{T}" /> for the specified type.
            /// </summary>
            /// <typeparam name="T">The type for which a shape is requested.</typeparam>
            /// <returns>
            /// The generated <see cref="global::PolyType.Abstractions.ITypeShape{T}" /> for the specified type.
            /// </returns>
            public global::PolyType.Abstractions.ITypeShape<T>? GetShape<T>()
                => (global::PolyType.Abstractions.ITypeShape<T>?)GetShape(typeof(T));

            """);

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
                    return {generatedType.Type.GeneratedPropertyName};

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

    private static SourceText FormatGenericProviderInterfaceImplementation(TypeDeclarationModel typeDeclaration, TypeShapeProviderModel provider)
    {
        var writer = new SourceWriter();
        StartFormatSourceFile(writer, typeDeclaration);

        writer.WriteLine($"{typeDeclaration.TypeDeclarationHeader} : global::PolyType.IShapeable<{typeDeclaration.Id.FullyQualifiedName}>");
        writer.WriteLine('{');
        writer.Indentation++;

        writer.WriteLine($"""
            static global::PolyType.Abstractions.ITypeShape<{typeDeclaration.Id.FullyQualifiedName}> global::PolyType.IShapeable<{typeDeclaration.Id.FullyQualifiedName}>.GetShape() 
                => {provider.Declaration.Id.FullyQualifiedName}.Default.{typeDeclaration.Id.GeneratedPropertyName};
            """);

        writer.Indentation--;
        writer.WriteLine('}');
        EndFormatSourceFile(writer);

        return writer.ToSourceText();
    }
}
