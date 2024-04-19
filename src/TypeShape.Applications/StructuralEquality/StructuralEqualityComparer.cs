using System.Collections;
using System.Diagnostics.CodeAnalysis;
using TypeShape.Abstractions;

namespace TypeShape.Applications.StructuralEquality;

public static partial class StructuralEqualityComparer
{
    public static IEqualityComparer<T> Create<T>(ITypeShape<T> shape) =>
        new Builder().BuildEqualityComparer(shape);

    public static IEqualityComparer<T> Create<T>(ITypeShapeProvider provider) =>
        Create(provider.Resolve<T>());

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

    internal static IEqualityComparer Create(Type type, ITypeShapeProvider provider)
    {
        ITypeShape shape = provider.Resolve(type);
        ITypeShapeFunc builder = new Builder();
        return (IEqualityComparer)shape.Invoke(builder, null)!;
    }

    private static class EqualityComparerCache<T, TProvider> where TProvider : ITypeShapeProvider<T>
    {
        public static IEqualityComparer<T> Value => s_value ??= Create(TProvider.GetShape());
        private static IEqualityComparer<T>? s_value;
    }
}