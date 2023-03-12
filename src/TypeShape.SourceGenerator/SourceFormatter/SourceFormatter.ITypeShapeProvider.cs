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
        writer.WriteStartBlock();

        writer.WriteLine("""
                public global::TypeShape.IType<T>? GetShape<T>()
                    => (global::TypeShape.IType<T>?)GetShape(typeof(T));

                """);

        writer.WriteLine("public global::TypeShape.IType? GetShape(Type type)");
        writer.WriteStartBlock();
        foreach (TypeModel generatedType in provider.ProvidedTypes)
        {
            writer.WriteLine($"""
                if (type == typeof({generatedType.Id.FullyQualifiedName}))
                    return {generatedType.Id.GeneratedPropertyName};

                """);
        }

        writer.WriteLine("return null;");
        writer.WriteEndBlock();

        writer.WriteEndBlock();
        EndFormatSourceFile(writer);
        return writer.ToSourceText();
    }
}
