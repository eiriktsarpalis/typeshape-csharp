namespace TypeShape.Applications.Counter;

public static partial class Counter
{
    // Defines the simplest possible generic traversal application:
    // walks the object graph returning a count of the number of nodes encountered.

    public static Func<T?, long> Create<T>(ITypeShape<T> shape)
    {
        var visitor = new Visitor();
        return (Func<T?, long>)shape.Accept(visitor, null)!;
    }

    public static long GetCount<T>(T? value) where T : ITypeShapeProvider<T>
        => CounterCache<T, T>.Value(value);

    public static long GetCount<T, TProvider>(T? value) where TProvider : ITypeShapeProvider<T>
        => CounterCache<T, TProvider>.Value(value);

    private static class CounterCache<T, TProvider> where TProvider : ITypeShapeProvider<T>
    {
        public static Func<T?, long> Value => s_value ??= Create(TProvider.GetShape());
        private static Func<T?, long>? s_value;
    }
}
