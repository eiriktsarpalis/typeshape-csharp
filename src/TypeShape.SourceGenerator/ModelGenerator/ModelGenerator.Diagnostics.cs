using Microsoft.CodeAnalysis;

namespace TypeShape.SourceGenerator;

public sealed partial class ModelGenerator
{
    private void ReportDiagnostic(Diagnostic diagnostic) => _diagnostics.Add(diagnostic);

    private static DiagnosticDescriptor TypeNotSupported { get; } = new DiagnosticDescriptor(
        id: "TS0001",
        title: "Type shape generation not supported for type.",
        messageFormat: "Type shape generation not supported for type {0}.",
        category: "TypeShape.SourceGenerator",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static DiagnosticDescriptor ProviderTypeNotPartial { get; } = new DiagnosticDescriptor(
        id: "TS0002",
        title: "Type annotated with GenerateShapeAttribute is not partial.",
        messageFormat: "The type {0} has been annotated with GenerateShapeAttribute but it or one of its parent types are not marked partial.",
        category: "TypeShape.SourceGenerator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
