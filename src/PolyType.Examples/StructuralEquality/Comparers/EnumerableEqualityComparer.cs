using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace PolyType.Examples.StructuralEquality.Comparers;

internal sealed class EnumerableEqualityComparer<TEnumerable, TElement> : EqualityComparer<TEnumerable>
{
    public required IEqualityComparer<TElement> ElementComparer { get; init; }
    public required Func<TEnumerable, IEnumerable<TElement>> GetEnumerable { get; init; }

    public override bool Equals(TEnumerable? x, TEnumerable? y)
    {
        if (x is null || y is null)
        {
            return x is null && y is null;
        }

        return GetEnumerable(x).SequenceEqual(GetEnumerable(y), ElementComparer);
    }

    public override int GetHashCode([DisallowNull] TEnumerable obj)
    {
        var hc = new HashCode();
        foreach (var element in GetEnumerable(obj))
        {
            hc.Add(element is null ? 0 : ElementComparer.GetHashCode(element));
        }

        return hc.ToHashCode();
    }
}