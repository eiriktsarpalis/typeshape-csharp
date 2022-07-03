namespace TypeShape.Applications.StructuralEquality;

public static partial class StructuralEqualityComparer
{
    public static IEqualityComparer<T> Create<T>(IType<T> shape)
    {
        var visitor = new Visitor();
        return (IEqualityComparer<T>)shape.Accept(visitor, null)!;
    }
}