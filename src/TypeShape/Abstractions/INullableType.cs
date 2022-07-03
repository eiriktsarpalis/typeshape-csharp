namespace TypeShape;

public interface INullableType
{
    IType NullableType { get; }
    IType ElementType { get; }
    object? Accept(INullableTypeVisitor visitor, object? state);
}

public interface INullableType<T> : INullableType
    where T : struct
{
}

public interface INullableTypeVisitor
{
    object? VisitNullable<T>(INullableType<T> nullableType, object? state) where T : struct;
}