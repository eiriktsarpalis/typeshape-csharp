using PolyType.Abstractions;
using PolyType.Utilities;
using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace PolyType.Examples.StructuralEquality.Comparers;

internal sealed class ObjectEqualityComparer<T> : EqualityComparer<T>
{
    public required EqualityComparer<T>[] PropertyComparers { get; init; }

    public override bool Equals(T? x, T? y)
    {
        if (x is null || y is null)
        {
            return x is null && y is null;
        }

        foreach (EqualityComparer<T> prop in PropertyComparers)
        {
            if (!prop.Equals(x, y))
            {
                return false;
            }
        }

        return true;
    }

    public override int GetHashCode([DisallowNull] T obj)
    {
        var hashCode = new HashCode();
        foreach (IEqualityComparer<T> prop in PropertyComparers)
        {
            hashCode.Add(prop.GetHashCode(obj));
        }

        return hashCode.ToHashCode();
    }
}

internal sealed class PropertyEqualityComparer<TDeclaringType, TPropertyType> : EqualityComparer<TDeclaringType>
{
    public required Getter<TDeclaringType, TPropertyType> Getter { get; init; }
    public required IEqualityComparer<TPropertyType> PropertyTypeEqualityComparer { get; init; }

    public override bool Equals(TDeclaringType? x, TDeclaringType? y)
    {
        Debug.Assert(x != null && y != null);
        return PropertyTypeEqualityComparer.Equals(Getter(ref x), Getter(ref y));
    }

    public override int GetHashCode([DisallowNull] TDeclaringType obj)
    {
        TPropertyType? propertyValue = Getter(ref obj);
        return propertyValue is not null ? PropertyTypeEqualityComparer.GetHashCode(propertyValue) : 0;
    }
}

internal sealed class PolymorphicObjectEqualityComparer(TypeCache cache) : EqualityComparer<object>
{
    public override bool Equals(object? x, object? y)
    {
        if (x is null || y is null)
        {
            return x is null && y is null;
        }

        Type runtimeType;
        if ((runtimeType = x.GetType()) != y.GetType())
        {
            return false;
        }

        if (runtimeType == typeof(object))
        {
            return true;
        }

        return GetComparer(runtimeType).Equals(x, y);
    }

    public override int GetHashCode([DisallowNull] object obj)
    {
        Type runtimeType = obj.GetType();
        return runtimeType == typeof(object) ? 1 : GetComparer(runtimeType).GetHashCode(obj);
    }

    private IEqualityComparer GetComparer(Type type) =>
        (IEqualityComparer)cache.GetOrAdd(type)!;
}
