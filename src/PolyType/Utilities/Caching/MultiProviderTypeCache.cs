using PolyType.Abstractions;
using System.Runtime.CompilerServices;

namespace PolyType.Utilities;

/// <summary>
/// Stores weakly referenced <see cref="TypeCache"/> instances keyed on <see cref="ITypeShapeProvider"/>.
/// </summary>
public sealed class MultiProviderTypeCache
{
    private readonly ConditionalWeakTable<ITypeShapeProvider, TypeCache> _providerCaches = new();
    private readonly ConditionalWeakTable<ITypeShapeProvider, TypeCache>.CreateValueCallback _createProviderCache;

    /// <summary>
    /// Initializes a new instance of the <see cref="MultiProviderTypeCache"/> class.
    /// </summary>
    public MultiProviderTypeCache()
    {
        _createProviderCache = provider => new(provider, this);
    }

    /// <summary>
    /// A factory method governing the creation of values when invoking the <see cref="GetOrAdd(ITypeShape)" /> method.
    /// </summary>
    public Func<TypeGenerationContext, ITypeShapeFunc>? ValueBuilderFactory { get; init; }

    /// <summary>
    /// A factory method governing delayed value initialization in case of recursive types.
    /// </summary>
    public IDelayedValueFactory? DelayedValueFactory { get; init; }

    /// <summary>
    /// Specifies whether exceptions should be cached.
    /// </summary>
    public bool CacheExceptions { get; init; }

    /// <summary>
    /// Gets or creates a cache scoped to the specified <paramref name="shapeProvider"/>.
    /// </summary>
    /// <param name="shapeProvider">The shape provider key.</param>
    /// <returns>A <see cref="TypeCache"/> scoped to <paramref name="shapeProvider"/>.</returns>
    public TypeCache GetScopedCache(ITypeShapeProvider shapeProvider)
    {
        ArgumentNullException.ThrowIfNull(shapeProvider);
        return _providerCaches.GetValue(shapeProvider, _createProviderCache);
    }

    /// <summary>
    /// Gets or adds a value keyed on the type represented by <paramref name="typeShape"/>.
    /// </summary>
    /// <param name="typeShape">The type shape representing the key type.</param>
    /// <returns>The final computed value.</returns>
    public object? GetOrAdd(ITypeShape typeShape)
    {
        ArgumentNullException.ThrowIfNull(typeShape);
        TypeCache cache = GetScopedCache(typeShape.Provider);
        return cache.GetOrAdd(typeShape);
    }

    /// <summary>
    /// Gets or adds a value keyed on the type represented by <paramref name="provider"/>.
    /// </summary>
    /// <param name="type">The type representing the key type.</param>
    /// <param name="provider">The type shape provider used to resolve the type shape.</param>
    /// <returns>The final computed value.</returns>
    public object? GetOrAdd(Type type, ITypeShapeProvider provider)
    {
        TypeCache cache = GetScopedCache(provider);
        return cache.GetOrAdd(type);
    }
}