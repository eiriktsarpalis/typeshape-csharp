namespace TypeShape.Applications.PrettyPrinter;

using System.Text;
using TypeShape;

public delegate void PrettyPrinter<T>(StringBuilder builder, T value);

public static partial class PrettyPrinter
{
    private readonly static Visitor s_Builder = new();

    public static PrettyPrinter<T> CreatePrinter<T>(IType<T> type)
    {
        return (PrettyPrinter<T>)s_Builder.VisitType(type, null)!;
    }

    public static string PrettyPrint<T>(this PrettyPrinter<T> pp, T value)
    {
        var sb = new StringBuilder();
        pp(sb, value);
        return sb.ToString();
    }
}
