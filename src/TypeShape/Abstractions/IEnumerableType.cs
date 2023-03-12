namespace TypeShape;

public interface IEnumerableType
{
    IType Type { get; }
    IType ElementType { get; }
    bool IsMutable { get; }
    object? Accept(IEnumerableTypeVisitor visitor, object? state);
}

public interface IEnumerableType<TEnumerable, TElement> : IEnumerableType
{
    Func<TEnumerable, IEnumerable<TElement>> GetGetEnumerable();
    Setter<TEnumerable, TElement> GetAddElement();
}

public interface IEnumerableTypeVisitor
{
    object? VisitEnumerableType<TEnumerable, TElement>(IEnumerableType<TEnumerable, TElement> enumerableType, object? state);
}