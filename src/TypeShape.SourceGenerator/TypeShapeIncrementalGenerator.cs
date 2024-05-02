using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TypeShape.Roslyn;
using TypeShape.SourceGenerator.Helpers;
using TypeShape.SourceGenerator.Model;

namespace TypeShape.SourceGenerator;

[Generator]
public sealed class TypeShapeIncrementalGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
#if LAUNCH_DEBUGGER
        if (!System.Diagnostics.Debugger.IsAttached)
        {
            System.Diagnostics.Debugger.Launch();
        }

#endif
        IncrementalValueProvider<TypeShapeKnownSymbols> knownSymbols = context.CompilationProvider
            .Select((compilation, _) => new TypeShapeKnownSymbols(compilation));

        IncrementalValuesProvider<TypeShapeProviderModel> generateShapeOfTModels = context.SyntaxProvider
            .ForTypesWithAttributeDeclaration(
                "TypeShape.GenerateShapeAttribute<T>",
                (node, _) => node is ClassDeclarationSyntax)
            .Combine(knownSymbols)
            .Select((tuple, cancellationToken) =>
                Parser.ParseFromGenerateShapeOfTAttributes(
                    context: tuple.Left,
                    knownSymbols: tuple.Right,
                    cancellationToken));


        // I kinda expect this to break the whole incremental pipeline
        var holder = new RoslynHelpers.MarkerAttributeHolder();
        context.RegisterAdditionalMarkerAttributeName(holder);
        IncrementalValuesProvider<TypeWithAttributeDeclarationContext> extraModels = context.SyntaxProvider
            .ForTypesWithOptionalAttributeDeclaration(holder,
                (node, _) => node is TypeDeclarationSyntax);

        IncrementalValueProvider<TypeShapeProviderModel?> generateDataModels = context.SyntaxProvider
            .ForTypesWithAttributeDeclaration(
                "TypeShape.GenerateShapeAttribute",
                (node, _) => node is TypeDeclarationSyntax)
            .Concat(extraModels)
            .Collect()
            .Combine(knownSymbols)
            .Select((tuple, token) => Parser.ParseFromGenerateShapeAttributes(tuple.Left, tuple.Right, token));


        context.RegisterSourceOutput(generateShapeOfTModels, GenerateSource);
        context.RegisterSourceOutput(generateDataModels ,  GenerateSource);
    }

    private void GenerateSource(SourceProductionContext context, TypeShapeProviderModel? provider)
    {
        if (provider is null)
        {
            return;
        }

        OnGeneratingSource?.Invoke(provider);

        foreach (EquatableDiagnostic diagnostic in provider.Diagnostics)
        {
            context.ReportDiagnostic(diagnostic.CreateDiagnostic());
        }

        SourceFormatter.FormatProvider(context, provider);
    }

    public Action<TypeShapeProviderModel>? OnGeneratingSource { get; init; }
}
