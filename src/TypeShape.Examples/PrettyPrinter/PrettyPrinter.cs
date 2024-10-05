using System.Text;
using TypeShape.Abstractions;

namespace TypeShape.Examples.PrettyPrinter;

public delegate void PrettyPrinter<T>(StringBuilder builder, int indentation, T? value);

public static partial class PrettyPrinter
{
    public static PrettyPrinter<T> Create<T>(ITypeShape<T> type)
        => new Builder().BuildPrettyPrinter(type);

    public static PrettyPrinter<T> Create<T>(ITypeShapeProvider provider)
        => Create(provider.Resolve<T>());

    public static string Print<T>(this PrettyPrinter<T> pp, T? value)
    {
        var sb = new StringBuilder();
        pp(sb, 0, value);
        return sb.ToString();
    }

    public static string Print<T>(T? value) where T : IShapeable<T>
        => PrettyPrinterCache<T, T>.Value.Print(value);

    public static string Print<T, TProvider>(T? value) where TProvider : IShapeable<T>
        => PrettyPrinterCache<T, TProvider>.Value.Print(value);

    private static class PrettyPrinterCache<T, TProvider> where TProvider : IShapeable<T>
    {
        public static PrettyPrinter<T> Value => s_value ??= Create(TProvider.GetShape());
        private static PrettyPrinter<T>? s_value;
    }
}
