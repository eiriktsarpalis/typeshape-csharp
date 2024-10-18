using System.Diagnostics.CodeAnalysis;
using TypeShape.Examples.Utilities;

namespace TypeShape.Examples.StructuralEquality.Comparers;

internal sealed class DelayedEqualityComparer<T>(ResultBox<IEqualityComparer<T>> self) : EqualityComparer<T>
{
    public override bool Equals(T? x, T? y) => self.Result.Equals(x, y);
    public override int GetHashCode([DisallowNull] T obj) => self.Result.GetHashCode(obj);
}