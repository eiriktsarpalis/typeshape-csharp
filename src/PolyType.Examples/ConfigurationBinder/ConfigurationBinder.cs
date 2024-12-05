using Microsoft.Extensions.Configuration;
using PolyType.Abstractions;
using PolyType.Utilities;
using System.Diagnostics.CodeAnalysis;

namespace PolyType.Examples.ConfigurationBinder;

/// <summary>Defines an <see cref="IConfiguration"/> binder build on top of PolyType.</summary>
public static partial class ConfigurationBinderTS
{
    private static readonly MultiProviderTypeCache s_cache = new()
    {
        DelayedValueFactory = new DelayedConfigurationBinderFactory(),
        ValueBuilderFactory = ctx => new Builder(ctx),
    };

    /// <summary>
    /// Builds a configuration binder delegate instance from the specified shape.
    /// </summary>
    /// <typeparam name="T">The type for which to build the binder.</typeparam>
    /// <param name="shape">The shape instance guiding binder construction.</param>
    /// <returns>A configuration binder delegate.</returns>
    public static Func<IConfiguration, T?> Create<T>(ITypeShape<T> shape) => 
        (Func<IConfiguration, T?>)s_cache.GetOrAdd(shape)!;

    /// <summary>
    /// Builds a configuration binder delegate instance from the specified shape provider.
    /// </summary>
    /// <typeparam name="T">The type for which to build the binder.</typeparam>
    /// <param name="shapeProvider">The shape provider guiding binder construction.</param>
    /// <returns>A configuration binder delegate.</returns>
    public static Func<IConfiguration, T?> Create<T>(ITypeShapeProvider shapeProvider) =>
        (Func<IConfiguration, T?>)s_cache.GetOrAdd(typeof(T), shapeProvider)!;

    /// <summary>
    /// Builds a configuration binder delegate instance from the specified shape provider.
    /// </summary>
    /// <typeparam name="T">The type for which to build the binder.</typeparam>
    /// <returns>A configuration binder delegate.</returns>
    [RequiresUnreferencedCode("PolyType reflection provider requires unreferenced code")]
    [RequiresDynamicCode("PolyType reflection provider requires dynamic code")]
    public static Func<IConfiguration, T?> Create<T>() => Create<T>(ReflectionProvider.ReflectionTypeShapeProvider.Default);

#if NET
    /// <summary>
    /// Binds an <see cref="IConfiguration"/> to the specified type using its <see cref="IShapeable{T}"/> implementation.
    /// </summary>
    /// <typeparam name="T">The type to which to bind the configuration.</typeparam>
    /// <param name="configuration">The instance providing the configuration.</param>
    /// <returns>An instance of <typeparamref name="T"/> that binds the configuration.</returns>
    public static T? Get<T>(IConfiguration configuration) where T : IShapeable<T>
        => ConfigurationBinderCache<T, T>.Value(configuration);

    /// <summary>
    /// Binds an <see cref="IConfiguration"/> to the specified type using an externally provided <see cref="IShapeable{T}"/> implementation.
    /// </summary>
    /// <typeparam name="T">The type to which to bind the configuration.</typeparam>
    /// <typeparam name="TProvider">The type providing an <see cref="IShapeable{T}"/> implementation.</typeparam>
    /// <param name="configuration">The instance providing the configuration.</param>
    /// <returns>An instance of <typeparamref name="T"/> that binds the configuration.</returns>
    public static T? Get<T, TProvider>(IConfiguration configuration) where TProvider : IShapeable<T>
        => ConfigurationBinderCache<T, TProvider>.Value(configuration);

    private static class ConfigurationBinderCache<T, TProvider> where TProvider : IShapeable<T>
    {
        public static Func<IConfiguration, T?> Value => s_value ??= Create(TProvider.GetShape());
        private static Func<IConfiguration, T?>? s_value;
    }
#endif
}