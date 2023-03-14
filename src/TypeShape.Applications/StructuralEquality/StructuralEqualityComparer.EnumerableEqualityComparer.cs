using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace TypeShape.Applications.StructuralEquality;

public static partial class StructuralEqualityComparer
{
    private sealed class EnumerableEqualityComparer<TEnumerable, TElement> : EqualityComparer<TEnumerable>
    {
        public IEqualityComparer<TElement>? ElementComparer { get; set; }
        public Func<TEnumerable, IEnumerable<TElement>>? GetEnumerable { get; set; }

        public override bool Equals(TEnumerable? x, TEnumerable? y)
        {
            Debug.Assert(ElementComparer != null);
            Debug.Assert(GetEnumerable != null);

            if (x is null || y is null)
            {
                return x is null && y is null;
            }

            return GetEnumerable(x).SequenceEqual(GetEnumerable(y), ElementComparer);
        }

        public override int GetHashCode([DisallowNull] TEnumerable obj)
        {
            Debug.Assert(ElementComparer != null);
            Debug.Assert(GetEnumerable != null);

            var hc = new HashCode();
            foreach (var element in GetEnumerable(obj))
            {
                hc.Add(element is null ? 0 : ElementComparer.GetHashCode(element));
            }

            return hc.ToHashCode();
        }
    }
}
