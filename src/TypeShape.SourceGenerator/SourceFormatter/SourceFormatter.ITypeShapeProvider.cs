using Microsoft.CodeAnalysis.Text;
using TypeShape.SourceGenerator.Helpers;
using TypeShape.SourceGenerator.Model;

namespace TypeShape.SourceGenerator;

internal static partial class SourceFormatter
{
    private static SourceText FormatProviderInterfaceImplementation(TypeShapeProviderModel provider)
    {
        var writer = new SourceWriter();
        StartFormatSourceFile(writer, provider);

        writer.WriteLine($"{provider.TypeDeclaration} : global::TypeShape.ITypeShapeProvider");
        writer.WriteLine('{');
        writer.Indentation++;

        writer.WriteLine("""
                public global::TypeShape.ITypeShape<T>? GetShape<T>()
                    => (global::TypeShape.ITypeShape<T>?)GetShape(typeof(T));

                """);

        writer.WriteLine("public global::TypeShape.ITypeShape? GetShape(Type type)");
        writer.WriteLine('{');
        writer.Indentation++;

        foreach (TypeModel generatedType in provider.ProvidedTypes)
        {
            writer.WriteLine($"""
                if (type == typeof({generatedType.Id.FullyQualifiedName}))
                    return {generatedType.Id.GeneratedPropertyName};

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
}
