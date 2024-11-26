using Microsoft.CodeAnalysis.Text;
using PolyType.Roslyn;
using PolyType.SourceGenerator.Model;

namespace PolyType.SourceGenerator;

internal sealed partial class SourceFormatter
{
    private static SourceText FormatShapeProviderMainFile(TypeShapeProviderModel provider)
    {
        var writer = new SourceWriter();
        StartFormatSourceFile(writer, provider.ProviderDeclaration);

        writer.WriteLine("""/// <summary>The source generated <see cref="global::PolyType.SourceGenModel.SourceGenTypeShapeProvider"/> implementation for the current assembly.</summary>""");
        writer.WriteLine($"""[global::System.CodeDom.Compiler.GeneratedCodeAttribute({FormatStringLiteral(PolyTypeGenerator.SourceGeneratorName)}, {FormatStringLiteral(PolyTypeGenerator.SourceGeneratorVersion)})]""");
        writer.WriteLine($"{provider.ProviderDeclaration.TypeDeclarationHeader} : global::PolyType.SourceGenModel.SourceGenTypeShapeProvider");
        writer.WriteLine('{');
        writer.Indentation++;

        writer.WriteLine($$"""
            private const global::System.Reflection.BindingFlags {{InstanceBindingFlagsConstMember}} = 
                global::System.Reflection.BindingFlags.Public | 
                global::System.Reflection.BindingFlags.NonPublic | 
                global::System.Reflection.BindingFlags.Instance;

            /// <summary>Gets the default instance of the <see cref="{{provider.ProviderDeclaration.Name}}"/> class.</summary>
            public static {{provider.ProviderDeclaration.Name}} {{ProviderSingletonProperty}} { get; } = new();

            /// <summary>Initializes a new instance of the <see cref="{{provider.ProviderDeclaration.Name}}"/> class.</summary>
            private {{provider.ProviderDeclaration.Name}}() { }
            """);

        writer.WriteLine();
        FormatGetShapeProviderMethod(provider, writer);

        writer.Indentation--;
        writer.WriteLine('}');
        EndFormatSourceFile(writer);

        return writer.ToSourceText();
    }

    private static void FormatGetShapeProviderMethod(TypeShapeProviderModel provider, SourceWriter writer)
    {
        writer.WriteLine("""
            /// <inheritdoc/>
            public override global::PolyType.Abstractions.ITypeShape? GetShape(global::System.Type type)
            {
            """);

        writer.Indentation++;

        foreach (TypeShapeModel generatedType in provider.ProvidedTypes.Values)
        {
            writer.WriteLine($$"""
                if (type == typeof({{generatedType.Type.FullyQualifiedName}}))
                {
                    return {{generatedType.SourceIdentifier}};
                }

                """);
        }

        writer.WriteLine("return null;");
        writer.Indentation--;
        writer.WriteLine('}');
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

    private static SourceText FormatWitnessTypeMainFile(TypeDeclarationModel typeDeclaration, TypeShapeProviderModel provider)
    {
        var writer = new SourceWriter();
        StartFormatSourceFile(writer, typeDeclaration);

        writer.WriteLine($$"""
            {{typeDeclaration.TypeDeclarationHeader}}
            {
                /// <summary>Gets the source generated <see cref="global::PolyType.SourceGenModel.SourceGenTypeShapeProvider"/> corresponding to the current witness type.</summary>
                public static global::PolyType.SourceGenModel.SourceGenTypeShapeProvider ShapeProvider => {{provider.ProviderDeclaration.Id.FullyQualifiedName}}.{{ProviderSingletonProperty}};
            }
            """);

        EndFormatSourceFile(writer);

        return writer.ToSourceText();
    }
}
