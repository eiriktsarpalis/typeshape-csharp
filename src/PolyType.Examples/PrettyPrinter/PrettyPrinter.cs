using PolyType.Abstractions;
using PolyType.Utilities;
using System.Text;

namespace PolyType.Examples.PrettyPrinter;

/// <summary>A delegate that formats a pretty-printed value to a string builder.</summary>
public delegate void PrettyPrinter<T>(StringBuilder builder, int indentation, T? value);

/// <summary>Provides a pretty printer for .NET types built on top of PolyType.</summary>
public static partial class PrettyPrinter
{
    private static readonly MultiProviderTypeCache s_cache = new()
    {
        DelayedValueFactory = new DelayedPrettyPrinterFactory(),
        ValueBuilderFactory = ctx => new Builder(ctx),
    };

    /// <summary>
    /// Builds a <see cref="PrettyPrinter{T}"/> instance from the specified shape.
    /// </summary>
    /// <typeparam name="T">The type for which to build the printer.</typeparam>
    /// <param name="shape">The shape instance guiding printer construction.</param>
    /// <returns>An <see cref="PrettyPrinter{T}"/> instance.</returns>
    public static PrettyPrinter<T> Create<T>(ITypeShape<T> shape)
        => (PrettyPrinter<T>)s_cache.GetOrAdd(shape)!;

    /// <summary>
    /// Builds a <see cref="PrettyPrinter{T}"/> instance from the specified shape provider.
    /// </summary>
    /// <typeparam name="T">The type for which to build the printer.</typeparam>
    /// <param name="shapeProvider">The shape provider guiding printer construction.</param>
    /// <returns>An <see cref="PrettyPrinter{T}"/> instance.</returns>
    public static PrettyPrinter<T> Create<T>(ITypeShapeProvider shapeProvider)
        => Create(shapeProvider.Resolve<T>());

    /// <summary>
    /// Pretty prints the specified value to a string.
    /// </summary>
    /// <typeparam name="T">The type of the value to be pretty printed.</typeparam>
    /// <param name="prettyPrinter">The pretty-printer handling the formatting.</param>
    /// <param name="value">The value to be formatted.</param>
    /// <returns>A string containing a pretty-printed rendering of the value.</returns>
    public static string Print<T>(this PrettyPrinter<T> prettyPrinter, T? value)
    {
        var sb = new StringBuilder();
        prettyPrinter(sb, 0, value);
        return sb.ToString();
    }

    /// <summary>
    /// Pretty prints the specified value to a string using its <see cref="IShapeable{T}"/> implementation.
    /// </summary>
    /// <typeparam name="T">The type of the value to be pretty printed.</typeparam>
    /// <param name="value">The value to be formatted.</param>
    /// <returns>A string containing a pretty-printed rendering of the value.</returns>
    public static string Print<T>(T? value) where T : IShapeable<T>
        => PrettyPrinterCache<T, T>.Value.Print(value);

    /// <summary>
    /// Pretty prints the specified value to a string using an externally provided <see cref="IShapeable{T}"/> implementation.
    /// </summary>
    /// <typeparam name="T">The type of the value to be pretty printed.</typeparam>
    /// <typeparam name="TProvider">The type providing an <see cref="IShapeable{T}"/> implementation.</typeparam>
    /// <param name="value">The value to be formatted.</param>
    /// <returns>A string containing a pretty-printed rendering of the value.</returns>
    public static string Print<T, TProvider>(T? value) where TProvider : IShapeable<T>
        => PrettyPrinterCache<T, TProvider>.Value.Print(value);

    private static class PrettyPrinterCache<T, TProvider> where TProvider : IShapeable<T>
    {
        public static PrettyPrinter<T> Value => s_value ??= Create(TProvider.GetShape());
        private static PrettyPrinter<T>? s_value;
    }
}
