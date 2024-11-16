namespace PolyType.SourceGenerator;

/// <summary>
/// Target framework inferred by the generator. Underlying enum values must be monotonic.
/// </summary>
public enum TargetFramework
{
    Netstandard20 = 20, // Placeholder value, not yet supported.
    Net80 = 80, // The default baseline supported by the generator.
    Net90 = 90,
}
