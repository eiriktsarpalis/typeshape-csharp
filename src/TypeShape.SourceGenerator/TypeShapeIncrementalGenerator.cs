using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TypeShape.SourceGenerator.Helpers;
using TypeShape.SourceGenerator.Model;

namespace TypeShape.SourceGenerator;

[Generator]
public sealed class TypeShapeIncrementalGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
#if LAUNCH_DEBUGGER
        System.Diagnostics.Debugger.Launch();
#endif
        IncrementalValueProvider<KnownSymbols> knownSymbols = context.CompilationProvider
            .Select((compilation, _) => new KnownSymbols(compilation));

        IncrementalValuesProvider<TypeShapeProviderModel> generationModels = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "TypeShape.GenerateShapeAttribute",
                (node, _) => node is ClassDeclarationSyntax,
                (context, _) => (ClassDeclarationSyntax: (ClassDeclarationSyntax)context.TargetNode, context.SemanticModel))
            .Combine(knownSymbols)
            .Select((tuple, token) => ModelGenerator.Compile(tuple.Right, tuple.Left.ClassDeclarationSyntax, tuple.Left.SemanticModel, token));

        context.RegisterSourceOutput(generationModels, GenerateSource);
    }

    private void GenerateSource(SourceProductionContext context, TypeShapeProviderModel provider)
    {
        OnGeneratingSource?.Invoke(provider);

        foreach (DiagnosticInfo diagnostic in provider.Diagnostics)
        {
            context.ReportDiagnostic(diagnostic.CreateDiagnostic());
        }

        SourceFormatter.FormatProvider(context, provider);
    }

    public Action<TypeShapeProviderModel>? OnGeneratingSource { get; init; }
}
