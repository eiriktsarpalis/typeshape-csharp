using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;

namespace TypeShape.SourceGenerator.Helpers;

internal static partial class RoslynHelpers
{
    public static bool TryGetAdditionalMarkerAttributeName(this IncrementalGeneratorInitializationContext context,
        [MaybeNullWhen(false)] out string attributeName)
    {
        string? configOption = null;
        var valueProvider = context.AnalyzerConfigOptionsProvider
            .Select((options, _) =>
            {
                if (options.GlobalOptions
                    .TryGetValue("build_property.TypeShape_SourceGenerator_AdditionalMarkerAttributeName",
                        out var markerAttribute))
                    configOption = markerAttribute;

                return 0;
            });
        context.RegisterSourceOutput(valueProvider, (_, __) => { });

        attributeName = configOption;
        return configOption is not null;
    }
}