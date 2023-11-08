namespace TypeShape.Applications.StructuralEquality;

public static partial class StructuralEqualityComparer
{
    public static IEqualityComparer<T> Create<T>(ITypeShape<T> shape)
    {
        var visitor = new Visitor();
        return (IEqualityComparer<T>)shape.Accept(visitor, null)!;
    }

    public static IEqualityComparer<T> Create<T>() where T : ITypeShapeProvider<T>
        => Create(T.GetShape());

    public static IEqualityComparer<T> Create<T, TProvider>() where TProvider : ITypeShapeProvider<T>
        => Create(TProvider.GetShape());
}