using System.Reflection;
using System.Runtime.CompilerServices;

namespace PolyType.ReflectionProvider;

/// <summary>
/// Exposes configuration options for the reflection-based type shape provider.
/// </summary>
public sealed record ReflectionTypeShapeProviderOptions
{
    /// <summary>
    /// Gets the default configuration options.
    /// </summary>
    public static ReflectionTypeShapeProviderOptions Default { get; } = new();

    /// <summary>
    /// Gets a value indicating whether System.Reflection.Emit should be used when generating member accessors.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>true</c> if the runtime supports dynamic code generation.
    /// </remarks>
    public bool UseReflectionEmit { get; init; } = ReflectionHelpers.IsDynamicCodeSupported;
}
