namespace TypeShape;

public interface IEnumerableType
{
    IType EnumerableType { get; }
    IType ElementType { get; }
    bool IsMutable { get; }
    object? Accept(IEnumerableTypeVisitor visitor, object? state);
}

public interface IEnumerableType<TEnumerable, TElement> : IEnumerableType
{
    Func<TEnumerable, IEnumerable<TElement>> GetGetEnumerable();
    Setter<TEnumerable, TElement> GetAddDelegate();
}

public interface IEnumerableTypeVisitor
{
    object? VisitEnumerableType<TEnumerable, TElement>(IEnumerableType<TEnumerable, TElement> enumerableType, object? state);
}