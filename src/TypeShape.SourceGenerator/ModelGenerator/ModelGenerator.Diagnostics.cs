using Microsoft.CodeAnalysis;
using TypeShape.SourceGenerator.Helpers;

namespace TypeShape.SourceGenerator;

public sealed partial class ModelGenerator
{
    private void ReportDiagnostic(DiagnosticDescriptor descriptor, Location? location, params object?[]? messageArgs)
    {
        _diagnostics.Add(new DiagnosticInfo
        { 
            Descriptor = descriptor, 
            Location = location?.GetLocationTrimmed(), 
            MessageArgs = messageArgs ?? [],
        });
    }

    private static DiagnosticDescriptor TypeNotSupported { get; } = new DiagnosticDescriptor(
        id: "TS0001",
        title: "Type shape generation not supported for type.",
        messageFormat: "Type shape generation not supported for type '{0}'.",
        category: "TypeShape.SourceGenerator",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static DiagnosticDescriptor ProviderTypeNotPartial { get; } = new DiagnosticDescriptor(
        id: "TS0002",
        title: "Type annotated with GenerateShapeAttribute is not partial.",
        messageFormat: "The type '{0}' has been annotated with GenerateShapeAttribute but it or one of its parent types are not marked partial.",
        category: "TypeShape.SourceGenerator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
