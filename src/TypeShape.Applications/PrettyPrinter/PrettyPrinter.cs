namespace TypeShape.Applications.PrettyPrinter;

using System.Text;
using TypeShape;

public delegate void PrettyPrinter<T>(StringBuilder builder, int indentation, T? value);

public static partial class PrettyPrinter
{
    private readonly static Visitor s_Builder = new();

    public static PrettyPrinter<T> Create<T>(IType<T> type)
    {
        return (PrettyPrinter<T>)type.Accept(s_Builder, null)!;
    }

    public static string PrettyPrint<T>(this PrettyPrinter<T> pp, T? value)
    {
        var sb = new StringBuilder();
        pp(sb, 0, value);
        return sb.ToString();
    }
}
