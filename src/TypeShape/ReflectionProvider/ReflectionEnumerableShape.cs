using System.Collections;
using System.Diagnostics;
using System.Reflection;

namespace TypeShape.ReflectionProvider;

internal class ReflectionEnumerableShape<TEnumerable, TElement> : IEnumerableShape<TEnumerable, TElement>
    where TEnumerable : IEnumerable<TElement>
{
    private readonly ReflectionTypeShapeProvider _provider;

    public ReflectionEnumerableShape(ReflectionTypeShapeProvider provider)
        => _provider = provider;

    public ITypeShape Type => _provider.GetShape<TEnumerable>();
    public ITypeShape ElementType => _provider.GetShape<TElement>();

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

    public object? Accept(ITypeShapeVisitor visitor, object? state)
        => visitor.VisitEnumerable(this, state);

    public virtual Setter<TEnumerable, TElement> GetAddElement()
    {
        if (!IsMutable)
        {
            throw new InvalidOperationException("The current enumerable shape does not support mutation.");
        }

        Debug.Assert(_addMethod != null);
        return _provider.MemberAccessor.CreateEnumerableAddDelegate<TEnumerable, TElement>(_addMethod);
    }

    public Func<TEnumerable, IEnumerable<TElement>> GetGetEnumerable()
        => static enumerable => enumerable;
}

internal sealed class ReflectionCollectionShape<TEnumerable, TElement> : ReflectionEnumerableShape<TEnumerable, TElement>
    where TEnumerable : ICollection<TElement>
{
    public ReflectionCollectionShape(ReflectionTypeShapeProvider provider) : base(provider) { }
    public override bool IsMutable => true;
    public override Setter<TEnumerable, TElement> GetAddElement()
        => static (ref TEnumerable enumerable, TElement element) => enumerable.Add(element);
}

internal class ReflectionEnumerableShape<TEnumerable> : IEnumerableShape<TEnumerable, object?>
    where TEnumerable : IEnumerable
{
    private readonly ReflectionTypeShapeProvider _provider;

    public ReflectionEnumerableShape(ReflectionTypeShapeProvider provider)
        => _provider = provider;

    public ITypeShape Type => _provider.GetShape<TEnumerable>();
    public ITypeShape ElementType => _provider.GetShape<object>();

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

    public object? Accept(ITypeShapeVisitor visitor, object? state)
        => visitor.VisitEnumerable(this, state);

    public virtual Setter<TEnumerable, object?> GetAddElement()
    {
        if (!IsMutable)
        {
            throw new InvalidOperationException("The current enumerable shape does not support mutation.");
        }

        Debug.Assert(_addMethod != null);
        return _provider.MemberAccessor.CreateEnumerableAddDelegate<TEnumerable, object?>(_addMethod);
    }

    public Func<TEnumerable, IEnumerable<object?>> GetGetEnumerable()
        => static enumerable => enumerable.Cast<object?>();
}

internal sealed class ReflectionListShape<TEnumerable> : ReflectionEnumerableShape<TEnumerable>
    where TEnumerable : IList
{
    public ReflectionListShape(ReflectionTypeShapeProvider provider) : base(provider) { }
    public override bool IsMutable => true;
    public override Setter<TEnumerable, object?> GetAddElement()
        => static (ref TEnumerable enumerable, object? value) => enumerable.Add(value);
}
