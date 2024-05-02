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

        IncrementalValueProvider<TypeShapeProviderModel?> generateDataModels = context.SyntaxProvider
            .ForTypesWithAttributeDeclaration(
                "TypeShape.GenerateShapeAttribute",
                (node, _) => node is TypeDeclarationSyntax)
            .Collect()
            .Combine(knownSymbols)
            .Select((tuple, token) => Parser.ParseFromGenerateShapeAttributes(tuple.Left, tuple.Right, token));

        if (context.TryGetAdditionalMarkerAttributeName(out var attributeName))
        {

            IncrementalValueProvider<TypeShapeProviderModel?> extraModels = context.SyntaxProvider
                .ForTypesWithAttributeDeclaration(
                    attributeName,
                    (node, _) => node is TypeDeclarationSyntax)
                .Collect()
                .Combine(knownSymbols)
                .Select((tuple, token) => Parser.ParseFromGenerateShapeAttributes(tuple.Left, tuple.Right, token));
            context.RegisterSourceOutput(extraModels, GenerateSource);
        }

        context.RegisterSourceOutput(generateShapeOfTModels, GenerateSource);
        context.RegisterSourceOutput(generateDataModels, GenerateSource);
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
