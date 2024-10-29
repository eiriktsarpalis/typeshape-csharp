using Microsoft.CodeAnalysis.Text;
using TypeShape.Roslyn;
using TypeShape.SourceGenerator.Model;

namespace TypeShape.SourceGenerator;

internal static partial class SourceFormatter
{
    private static SourceText FormatProviderInterfaceImplementation(TypeShapeProviderModel provider)
    {
        var writer = new SourceWriter();
        StartFormatSourceFile(writer, provider.Declaration);

        writer.WriteLine($"{provider.Declaration.TypeDeclarationHeader} : global::TypeShape.ITypeShapeProvider");
        writer.WriteLine('{');
        writer.Indentation++;

        writer.WriteLine("""
            /// <summary>
            /// Gets the generated <see cref="global::TypeShape.Abstractions.ITypeShape{T}" /> for the specified type.
            /// </summary>
            /// <typeparam name="T">The type for which a shape is requested.</typeparam>
            /// <returns>
            /// The generated <see cref="global::TypeShape.Abstractions.ITypeShape{T}" /> for the specified type.
            /// </returns>
            public global::TypeShape.Abstractions.ITypeShape<T>? GetShape<T>()
                => (global::TypeShape.Abstractions.ITypeShape<T>?)GetShape(typeof(T));

            """);

        writer.WriteLine("""
            /// <summary>
            /// Gets the generated <see cref="global::TypeShape.Abstractions.ITypeShape" /> for the specified type.
            /// </summary>
            /// <param name="type">The type for which a shape is requested.</param>
            /// <returns>
            /// The generated <see cref="global::TypeShape.Abstractions.ITypeShape" /> for the specified type.
            /// </returns>
            public global::TypeShape.Abstractions.ITypeShape? GetShape(global::System.Type type)
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

        writer.WriteLine($"{typeDeclaration.TypeDeclarationHeader} : global::TypeShape.IShapeable<{typeDeclaration.Id.FullyQualifiedName}>");
        writer.WriteLine('{');
        writer.Indentation++;

        writer.WriteLine($"""
            static global::TypeShape.Abstractions.ITypeShape<{typeDeclaration.Id.FullyQualifiedName}> global::TypeShape.IShapeable<{typeDeclaration.Id.FullyQualifiedName}>.GetShape() 
                => {provider.Declaration.Id.FullyQualifiedName}.Default.{typeDeclaration.Id.GeneratedPropertyName};
            """);

        writer.Indentation--;
        writer.WriteLine('}');
        EndFormatSourceFile(writer);

        return writer.ToSourceText();
    }
}
