using System.Collections;
using System.Diagnostics;
using System.Reflection;

namespace TypeShape.ReflectionProvider;

internal class ReflectionEnumerableType<TEnumerable, TElement> : IEnumerableType<TEnumerable, TElement>
    where TEnumerable : IEnumerable<TElement>
{
    private readonly ReflectionTypeShapeProvider _provider;

    public ReflectionEnumerableType(ReflectionTypeShapeProvider provider)
        => _provider = provider;

    public IType Type => _provider.GetShape<TEnumerable>();
    public IType ElementType => _provider.GetShape<TElement>();

    public virtual bool IsMutable => _isMutable ??= DetermineIsMutable();
    private MethodInfo? _addMethod;
    private bool? _isMutable;

    private bool DetermineIsMutable()
    {
        foreach (MethodInfo methodInfo in typeof(TEnumerable).GetMethods(BindingFlags.Public | BindingFlags.Instance))
        {
            if (methodInfo.Name is "Add" or "Enqueue" or "Push" &&
                methodInfo.ReturnType == typeof(void) && 
                methodInfo.GetParameters() is [ParameterInfo parameter] &&
                parameter.ParameterType == typeof(TElement))
            {
                _addMethod = methodInfo;
                return true;
            }
        }

        return false;
    }

    public object? Accept(IEnumerableTypeVisitor visitor, object? state)
        => visitor.VisitEnumerableType(this, state);

    public virtual Setter<TEnumerable, TElement> GetAddElement()
    {
        if (!IsMutable)
        {
            throw new InvalidOperationException();
        }

        Debug.Assert(_addMethod != null);
        return _provider.MemberAccessor.CreateEnumerableAddDelegate<TEnumerable, TElement>(_addMethod);
    }

    public Func<TEnumerable, IEnumerable<TElement>> GetGetEnumerable()
        => static enumerable => enumerable;
}

internal sealed class ReflectionCollectionType<TEnumerable, TElement> : ReflectionEnumerableType<TEnumerable, TElement>
    where TEnumerable : ICollection<TElement>
{
    public ReflectionCollectionType(ReflectionTypeShapeProvider provider) : base(provider) { }
    public override bool IsMutable => true;
    public override Setter<TEnumerable, TElement> GetAddElement()
        => static (ref TEnumerable enumerable, TElement element) => enumerable.Add(element);
}

internal class ReflectionEnumerableType<TEnumerable> : IEnumerableType<TEnumerable, object?>
    where TEnumerable : IEnumerable
{
    private readonly ReflectionTypeShapeProvider _provider;

    public ReflectionEnumerableType(ReflectionTypeShapeProvider provider)
        => _provider = provider;

    public IType Type => _provider.GetShape(typeof(TEnumerable));
    public IType ElementType => _provider.GetShape(typeof(object));

    public virtual bool IsMutable => _isMutable ??= DetermineIsMutable();
    private MethodInfo? _addMethod;
    private bool? _isMutable;

    private bool DetermineIsMutable()
    {
        foreach (MethodInfo methodInfo in typeof(TEnumerable).GetMethods(BindingFlags.Public | BindingFlags.Instance))
        {
            if (methodInfo.Name is "Add" or "Enqueue" or "Push" &&
                methodInfo.ReturnType == typeof(void) &&
                methodInfo.GetParameters() is [ParameterInfo parameter] &&
                parameter.ParameterType == typeof(object))
            {
                _addMethod = methodInfo;
                return true;
            }
        }

        return false;
    }

    public object? Accept(IEnumerableTypeVisitor visitor, object? state)
        => visitor.VisitEnumerableType(this, state);

    public virtual Setter<TEnumerable, object?> GetAddElement()
    {
        if (!IsMutable)
        {
            throw new InvalidOperationException();
        }

        Debug.Assert(_addMethod != null);
        return _provider.MemberAccessor.CreateEnumerableAddDelegate<TEnumerable, object?>(_addMethod);
    }

    public Func<TEnumerable, IEnumerable<object?>> GetGetEnumerable()
        => static enumerable => enumerable.Cast<object?>();
}

internal sealed class ReflectionListType<TEnumerable> : ReflectionEnumerableType<TEnumerable>
    where TEnumerable : IList
{
    public ReflectionListType(ReflectionTypeShapeProvider provider) : base(provider) { }
    public override bool IsMutable => true;
    public override Setter<TEnumerable, object?> GetAddElement()
        => static (ref TEnumerable enumerable, object? value) => enumerable.Add(value);
}
