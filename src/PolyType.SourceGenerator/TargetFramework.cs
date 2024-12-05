namespace PolyType.SourceGenerator;

/// <summary>
/// Target framework inferred by the generator. Underlying enum values must be monotonic.
/// </summary>
public enum TargetFramework
{
    Legacy = 20, // netstandard, netfx, or older .NET Core targets.
    Net80 = 80, // The modern .NET baseline supported by PolyType.
    Net90 = 90,
}
