using System.Reflection;
using System.Runtime.CompilerServices;

namespace PolyType.ReflectionProvider;

/// <summary>
/// Exposes configuration options for the reflection-based type shape provider.
/// </summary>
public sealed class ReflectionTypeShapeProviderOptions
{
    /// <summary>
    /// Gets the default configuration options.
    /// </summary>
    public static ReflectionTypeShapeProviderOptions Default { get; } = new();

    /// <summary>
    /// Whether System.Reflection.Emit should be used when generating member accessors.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>true</c> if the runtime supports dynamic code generation.
    /// </remarks>
    public bool UseReflectionEmit { get; init; } = RuntimeFeature.IsDynamicCodeSupported;

    /// <summary>
    /// Whether the resolver should use <see cref="NullabilityInfoContext"/> to resolve nullable annotations.
    /// </summary>
    /// <remarks>
    /// Should be turned off in applications that disable <see cref="NullabilityInfoContext"/>
    /// via the NullabilityInfoContext.IsSupported feature switch (e.g. Blazor WebAssembly).
    /// </remarks>
    public bool ResolveNullableAnnotations { get; init; } = true;
}
