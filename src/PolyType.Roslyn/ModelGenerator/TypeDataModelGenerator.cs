using Microsoft.CodeAnalysis;
using PolyType.Roslyn.Helpers;
using System.Collections.Immutable;

namespace PolyType.Roslyn;

/// <summary>
/// Provides functionality and extensibility points for generating <see cref="TypeDataModel"/> 
/// instances for a given set of <see cref="ITypeSymbol"/> inputs.
/// </summary>
public partial class TypeDataModelGenerator
{
    private ImmutableDictionary<ITypeSymbol, TypeDataModel> _generatedModels;
    private List<EquatableDiagnostic>? _diagnostics;

    /// <summary>
    /// Creates a new <see cref="TypeDataModelGenerator"/> instance.
    /// </summary>
    /// <param name="generationScope">The context symbol used to determine accessibility for processed types.</param>
    /// <param name="knownSymbols">The known symbols cache constructed from the current <see cref="Compilation"/>.</param>
    /// <param name="cancellationToken">The cancellation token to be used by the generator.</param>
    public TypeDataModelGenerator(ISymbol generationScope, KnownSymbols knownSymbols, CancellationToken cancellationToken)
    {
        GenerationScope = generationScope;
        KnownSymbols = knownSymbols;
        CancellationToken = cancellationToken;
        _generatedModels = ImmutableDictionary.Create<ITypeSymbol, TypeDataModel>(SymbolComparer);
    }

    /// <summary>
    /// The context symbol used to determine accessibility for processed types.
    /// </summary>
    public ISymbol GenerationScope { get; }

    /// <summary>
    /// The default location to be used for diagnostics.
    /// </summary>
    public virtual Location? DefaultLocation => GenerationScope.Locations.FirstOrDefault();

    /// <summary>
    /// The known symbols cache constructed from the current <see cref="Compilation" />.
    /// </summary>
    public KnownSymbols KnownSymbols { get; }

    /// <summary>
    /// The cancellation token to be used by the generator.
    /// </summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>
    /// All generated models, keyed by their type symbol.
    /// </summary>
    public ImmutableDictionary<ITypeSymbol, TypeDataModel> GeneratedModels => _generatedModels;

    /// <summary>
    /// The <see cref="SymbolEqualityComparer"/> used to identify type symbols.
    /// Defaults to <see cref="SymbolEqualityComparer.Default"/>.
    /// </summary>
    public virtual SymbolEqualityComparer SymbolComparer => SymbolEqualityComparer.Default;

    /// <summary>
    /// Attempt to generate a <see cref="TypeDataModel"/> for the given <paramref name="type"/>.
    /// </summary>
    /// <param name="type">The type for which to generate a data model.</param>
    /// <returns>The generation status for the given type.</returns>
    public TypeDataModelGenerationStatus IncludeType(ITypeSymbol type)
    {
        ImmutableDictionary<ITypeSymbol, TypeDataModel> generatedModelsSnapshot = _generatedModels;
        var ctx = new TypeDataModelGenerationContext(ImmutableStack<ITypeSymbol>.Empty, generatedModelsSnapshot);
        TypeDataModelGenerationStatus status = IncludeNestedType(type, ref ctx);

        if (status is TypeDataModelGenerationStatus.Success)
        {
            if (Interlocked.CompareExchange(ref _generatedModels, ctx.GeneratedModels, generatedModelsSnapshot) != generatedModelsSnapshot)
            {
                throw new InvalidOperationException("Model generator is being called concurrently by another thread.");
            }
        }

        return status;
    }

    /// <summary>
    /// Gets the list of equatable diagnostics that have been recorded by this model generator.
    /// </summary>
    public List<EquatableDiagnostic> Diagnostics => _diagnostics ??= [];

    /// <summary>
    /// Adds a new diagnostic to the <see cref="Diagnostics"/> property.
    /// </summary>
    public void ReportDiagnostic(DiagnosticDescriptor descriptor, Location? location, params object?[] messageArgs)
    {
        if (location is not null && !KnownSymbols.Compilation.ContainsLocation(location))
        {
            // If the location is outside of the current compilation,
            // fall back to the default location for the generator.
            location = DefaultLocation;
        }

        Diagnostics.Add(new EquatableDiagnostic(descriptor, location, messageArgs));
    }

    /// <summary>
    /// When overridden, performs normalization operations on the given type before it is processed.
    /// </summary>
    protected virtual ITypeSymbol NormalizeType(ITypeSymbol type) => type;

    /// <summary>
    /// When overridden, resolves the requested kind of the type as specified by configuration.
    /// </summary>
    /// <param name="type">The type symbol for which to resolve the requested kind.</param>
    /// <returns>The requested kind for <paramref name="type"/>.</returns>
    protected virtual TypeDataKind? ResolveRequestedKind(ITypeSymbol type) => null;

    /// <summary>
    /// Wraps the <see cref="MapType(ITypeSymbol, ref TypeDataModelGenerationContext, out TypeDataModel?)"/> method
    /// with pre- and post-processing steps necessary for a type graph traversal.
    /// </summary>
    /// <param name="type">The type for which to generate a data model.</param>
    /// <param name="ctx">The context token holding state for the current type graph traversal.</param>
    /// <returns>The model generation status for the given type.</returns>
    protected TypeDataModelGenerationStatus IncludeNestedType(ITypeSymbol type, ref TypeDataModelGenerationContext ctx)
    {
        CancellationToken.ThrowIfCancellationRequested();

        type = NormalizeType(type);

        if (ctx.GeneratedModels.TryGetValue(type, out TypeDataModel? model))
        {
            model.IsRootType |= ctx.Stack.IsEmpty;
            return TypeDataModelGenerationStatus.Success;
        }

        if (!IsSupportedType(type))
        {
            return TypeDataModelGenerationStatus.UnsupportedType;
        }

        if (!IsAccessibleSymbol(type))
        {
            return TypeDataModelGenerationStatus.InaccessibleType;
        }

        if (ctx.Stack.Contains(type, SymbolComparer))
        {
            // Recursive type detected, skip with success
            return TypeDataModelGenerationStatus.Success;
        }

        // Create a new snapshot with the current type pushed onto the stack.
        // Only commit the generated model if the type is successfully mapped.
        TypeDataModelGenerationContext scopedCtx = ctx.Push(type);
        TypeDataModelGenerationStatus status = MapType(type, ref scopedCtx, out model);

        if (status is TypeDataModelGenerationStatus.Success != model is not null)
        {
            throw new InvalidOperationException($"The '{nameof(MapType)}' method returned inconsistent {nameof(TypeDataModelGenerationStatus)} and {nameof(TypeDataModel)} results.");
        }

        if (model != null)
        {
            ctx = scopedCtx.Commit(model);
            model.IsRootType = ctx.Stack.IsEmpty;
        }

        return status;
    }

    /// <summary>
    /// The core, overridable data model mapping method for a given type.
    /// </summary>
    /// <param name="type">The type for which to generate a data model.</param>
    /// <param name="ctx">The context token holding state for the current type graph traversal.</param>
    /// <param name="model">The model that the current symbol is being mapped to.</param>
    /// <returns>The model generation status for the given type.</returns>
    /// <remarks>
    /// The method should only be overridden but not invoked directly. 
    /// Call <see cref="IncludeNestedType(ITypeSymbol, ref TypeDataModelGenerationContext)"/> instead.
    /// </remarks>
    protected virtual TypeDataModelGenerationStatus MapType(ITypeSymbol type, ref TypeDataModelGenerationContext ctx, out TypeDataModel? model)
    {
        TypeDataModelGenerationStatus status;

        switch (ResolveRequestedKind(type))
        {
            // If the configuration specifies an explicit kind, try to resolve that or fall back to no shape.
            case TypeDataKind.Enum:
                if (TryMapEnum(type, ref ctx, out model, out status))
                {
                    return status;
                }
                goto None;

            case TypeDataKind.Nullable:
                if (TryMapNullable(type, ref ctx, out model, out status))
                {
                    return status;
                }
                goto None;

            case TypeDataKind.Dictionary:
                if (TryMapDictionary(type, ref ctx, out model, out status))
                {
                    return status;
                }
                goto None;

            case TypeDataKind.Enumerable:
                if (TryMapEnumerable(type, ref ctx, out model, out status))
                {
                    return status;
                }
                goto None;

            case TypeDataKind.Tuple:
                if (TryMapTuple(type, ref ctx, out model, out status))
                {
                    return status;
                }
                goto None;

            case TypeDataKind.Object:
                if (TryMapObject(type, ref ctx, out model, out status))
                {
                    return status;
                }
                goto None;

            case TypeDataKind.None:
                goto None;
        }

        if (TryMapEnum(type, ref ctx, out model, out status))
        {
            return status;
        }

        if (TryMapNullable(type, ref ctx, out model, out status))
        {
            return status;
        }

        // Important: Dictionary resolution goes before Enumerable
        // since Dictionary also implements IEnumerable
        if (TryMapDictionary(type, ref ctx, out model, out status))
        {
            return status;
        }

        if (TryMapEnumerable(type, ref ctx, out model, out status))
        {
            return status;
        }

        if (TryMapTuple(type, ref ctx, out model, out status))
        {
            return status;
        }

        if (TryMapObject(type, ref ctx, out model, out status))
        {
            return status;
        }

    None:
        // A supported type of unrecognized kind, do not include any metadata.
        model = new TypeDataModel { Type = type };
        return TypeDataModelGenerationStatus.Success;
    }

    /// <summary>
    /// Determines if the specified symbol is accessible within the <see cref="GenerationScope"/> symbol.
    /// </summary>
    protected virtual bool IsAccessibleSymbol(ISymbol symbol)
    {
        return KnownSymbols.Compilation.IsSymbolAccessibleWithin(symbol, within: GenerationScope);
    }

    /// <summary>
    /// Determines if the specified symbol is supported for data model generation.
    /// </summary>
    /// <remarks>
    /// By default, unsupported types are void, pointers, and generic type definitions.
    /// </remarks>
    protected virtual bool IsSupportedType(ITypeSymbol type)
    {
        return type.TypeKind is not (TypeKind.Pointer or TypeKind.Error) &&
          type.SpecialType is not SpecialType.System_Void && !type.ContainsGenericParameters();
    }
}
