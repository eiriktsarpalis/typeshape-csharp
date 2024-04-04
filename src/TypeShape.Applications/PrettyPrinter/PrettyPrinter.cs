namespace TypeShape.Applications.PrettyPrinter;

using System.Text;
using TypeShape;

public delegate void PrettyPrinter<T>(StringBuilder builder, int indentation, T? value);

public static partial class PrettyPrinter
{
    public static PrettyPrinter<T> Create<T>(ITypeShape<T> type)
    {
        var builder = new Builder();
        return builder.BuildPrettyPrinter(type);
    }

    public static string Print<T>(this PrettyPrinter<T> pp, T? value)
    {
        var sb = new StringBuilder();
        pp(sb, 0, value);
        return sb.ToString();
    }

    public static string Print<T>(T? value) where T : ITypeShapeProvider<T>
        => PrettyPrinterCache<T, T>.Value.Print(value);

    public static string Print<T, TProvider>(T? value) where TProvider : ITypeShapeProvider<T>
        => PrettyPrinterCache<T, TProvider>.Value.Print(value);

    private static class PrettyPrinterCache<T, TProvider> where TProvider : ITypeShapeProvider<T>
    {
        public static PrettyPrinter<T> Value => s_value ??= Create(TProvider.GetShape());
        private static PrettyPrinter<T>? s_value;
    }
}
