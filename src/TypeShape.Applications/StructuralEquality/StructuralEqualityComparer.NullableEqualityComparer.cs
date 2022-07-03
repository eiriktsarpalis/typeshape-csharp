using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace TypeShape.Applications.StructuralEquality;

public static partial class StructuralEqualityComparer
{
    private sealed class NullableEqualityComparer<T> : IEqualityComparer<T?>
        where T : struct
    {
        public IEqualityComparer<T>? ElementComparer { get; set; }
        public bool Equals(T? x, T? y)
        {
            Debug.Assert(ElementComparer != null);

            if (x is null || y is null)
            {
                return x is null && y is null;
            }

            return ElementComparer.Equals(x.Value, y.Value);
        }

        public int GetHashCode([DisallowNull] T? obj)
        {
            Debug.Assert(ElementComparer != null);
            return obj.HasValue ? ElementComparer.GetHashCode(obj.Value) : 0;
        }
    }
}
