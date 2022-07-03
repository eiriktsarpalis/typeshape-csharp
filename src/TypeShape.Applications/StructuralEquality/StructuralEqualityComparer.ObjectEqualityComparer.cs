using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace TypeShape.Applications.StructuralEquality;

public static partial class StructuralEqualityComparer
{
    private sealed class ObjectEqualityComparer<T> : IEqualityComparer<T>
    {
        public IEqualityComparer<T>[]? PropertyComparers { get; set; }

        public bool Equals(T? x, T? y)
        {
            Debug.Assert(PropertyComparers != null);

            if (x is null || y is null)
            {
                return x is null && y is null;
            }

            foreach (IEqualityComparer<T> prop in PropertyComparers)
            {
                if (!prop.Equals(x, y))
                {
                    return false;
                }
            }

            return true;
        }

        public int GetHashCode([DisallowNull] T obj)
        {
            Debug.Assert(PropertyComparers != null);

            var hashCode = new HashCode();
            foreach (IEqualityComparer<T> prop in PropertyComparers)
            {
                hashCode.Add(prop.GetHashCode(obj));
            }

            return hashCode.ToHashCode();
        }
    }

    private sealed class PropertyEqualityComparer<TDeclaringType, TPropertyType> : IEqualityComparer<TDeclaringType>
    {
        public required Getter<TDeclaringType, TPropertyType> Getter { get; init; }
        public required IEqualityComparer<TPropertyType> PropertyTypeEqualityComparer { get; init; }

        public bool Equals(TDeclaringType? x, TDeclaringType? y)
        {
            Debug.Assert(x != null && y != null);
            return PropertyTypeEqualityComparer.Equals(Getter(ref x), Getter(ref y));
        }

        public int GetHashCode([DisallowNull] TDeclaringType obj)
        {
            TPropertyType? propertyValue = Getter(ref obj);
            return propertyValue is not null ? PropertyTypeEqualityComparer.GetHashCode(propertyValue) : 0;
        }
    }
}
