using TypeShape.Abstractions;

namespace TypeShape.Examples.Counter;

public static partial class Counter
{
    // Defines the simplest possible generic traversal application:
    // walks the object graph returning a count of the number of nodes encountered.

    public static Func<T?, long> Create<T>(ITypeShape<T> shape)
        => new Builder().BuildCounter(shape);

    public static Func<T?, long> Create<T>(ITypeShapeProvider provider)
        => Create(provider.Resolve<T>());

    public static long GetCount<T>(T? value) where T : IShapeable<T>
        => CounterCache<T, T>.Value(value);

    public static long GetCount<T, TProvider>(T? value) where TProvider : IShapeable<T>
        => CounterCache<T, TProvider>.Value(value);

    private static class CounterCache<T, TProvider> where TProvider : IShapeable<T>
    {
        public static Func<T?, long> Value => s_value ??= Create(TProvider.GetShape());
        private static Func<T?, long>? s_value;
    }
}
