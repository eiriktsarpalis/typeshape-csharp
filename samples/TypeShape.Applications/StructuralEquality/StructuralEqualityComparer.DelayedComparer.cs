using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace TypeShape.Applications.StructuralEquality;

public partial class StructuralEqualityComparer
{
    public sealed class DelayedEqualityComparer<T>(ResultHolder<EqualityComparer<T>> holder) : EqualityComparer<T>
    {
        public IEqualityComparer<T> Underlying
        {
            get
            {
                Debug.Assert(holder.Value != null);
                return holder.Value;
            }
        }

        public override bool Equals(T? x, T? y) => Underlying.Equals(x, y);
        public override int GetHashCode([DisallowNull] T obj) => Underlying.GetHashCode(obj);
    }
}