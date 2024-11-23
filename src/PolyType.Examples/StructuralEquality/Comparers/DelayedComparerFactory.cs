using PolyType.Abstractions;
using PolyType.Utilities;
using System.Diagnostics.CodeAnalysis;

namespace PolyType.Examples.StructuralEquality.Comparers;

internal sealed class DelayedEqualityComparerFactory : IDelayedValueFactory
{
    public DelayedValue Create<T>(ITypeShape<T> typeShape) =>
        new DelayedValue<IEqualityComparer<T>>(self => new DelayedEqualityComparer<T>(self));

    private sealed class DelayedEqualityComparer<T>(DelayedValue<IEqualityComparer<T>> self) : EqualityComparer<T>
    {
        public override bool Equals(T? x, T? y) => self.Result.Equals(x, y);
        public override int GetHashCode([DisallowNull] T obj) => self.Result.GetHashCode(obj);
    }
}