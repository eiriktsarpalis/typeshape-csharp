using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TypeShape.SourceGenerator.Model;

namespace TypeShape.SourceGenerator;

[Generator]
public sealed class TypeShapeIncrementalGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
#if LAUNCH_DEBUGGER
        Debugger.Launch();
#endif
        IncrementalValuesProvider<TypeShapeProviderModel> generationModels = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "TypeShape.GenerateShapeAttribute",
                (node, _) => node is ClassDeclarationSyntax,
                (context, _) => (ClassDeclarationSyntax)context.TargetNode)
            .Combine(context.CompilationProvider)
            .Select((state, token) => ModelGenerator.Compile(state.Left, state.Right, token));

        context.RegisterSourceOutput(generationModels, OnModelCreated);
    }

    private static void OnModelCreated(SourceProductionContext context, TypeShapeProviderModel provider)
    {
        foreach (Diagnostic diagnostic in provider.Diagnostics)
            context.ReportDiagnostic(diagnostic);

        SourceFormatter.FormatProvider(context, provider);
    }
}
