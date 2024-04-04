using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace TypeShape.Applications.StructuralEquality;

public static partial class StructuralEqualityComparer
{
    public static IEqualityComparer<T> Create<T>(ITypeShape<T> shape)
    {
        var builder = new Builder();
        return builder.BuildEqualityComparer(shape);
    }

    public static IEqualityComparer Create(ITypeShape shape)
    {
        ITypeShapeFunc builder = new Builder();
        return (IEqualityComparer)shape.Invoke(builder, null)!;
    }

    public static IEqualityComparer<T> Create<T>() where T : ITypeShapeProvider<T> =>
        EqualityComparerCache<T, T>.Value;

    public static IEqualityComparer<T> Create<T, TProvider>() where TProvider : ITypeShapeProvider<T> => 
        EqualityComparerCache<T, TProvider>.Value;

    public static int GetHashCode<T>([DisallowNull] T value) where T : ITypeShapeProvider<T> => 
        EqualityComparerCache<T, T>.Value.GetHashCode(value);

    public static bool Equals<T>(T? left, T? right) where T : ITypeShapeProvider<T> => 
        EqualityComparerCache<T, T>.Value.Equals(left, right);

    public static int GetHashCode<T, TProvider>([DisallowNull] T value) where TProvider : ITypeShapeProvider<T> => 
        EqualityComparerCache<T, TProvider>.Value.GetHashCode(value);

    public static bool Equals<T, TProvider>(T? left, T? right) where TProvider : ITypeShapeProvider<T> => 
        EqualityComparerCache<T, TProvider>.Value.Equals(left, right);

    private static class EqualityComparerCache<T, TProvider> where TProvider : ITypeShapeProvider<T>
    {
        public static IEqualityComparer<T> Value => s_value ??= Create(TProvider.GetShape());
        private static IEqualityComparer<T>? s_value;
    }
}