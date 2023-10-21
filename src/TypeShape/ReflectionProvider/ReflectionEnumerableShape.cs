using System.Collections;
using System.Diagnostics;
using System.Reflection;

namespace TypeShape.ReflectionProvider;

internal class ReflectionEnumerableShape<TEnumerable, TElement>(ReflectionTypeShapeProvider provider) : IEnumerableShape<TEnumerable, TElement>
    where TEnumerable : IEnumerable<TElement>
{
    public ITypeShape Type => provider.GetShape<TEnumerable>();
    public ITypeShape ElementType => provider.GetShape<TElement>();

    public virtual bool IsMutable => _isMutable ??= DetermineIsMutable();
    public int Rank => 1;

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
        return provider.MemberAccessor.CreateEnumerableAddDelegate<TEnumerable, TElement>(_addMethod);
    }

    public Func<TEnumerable, IEnumerable<TElement>> GetGetEnumerable()
        => static enumerable => enumerable;
}

internal sealed class ReflectionCollectionShape<TEnumerable, TElement>(ReflectionTypeShapeProvider provider) : ReflectionEnumerableShape<TEnumerable, TElement>(provider)
    where TEnumerable : ICollection<TElement>
{
    public override bool IsMutable => true;
    public override Setter<TEnumerable, TElement> GetAddElement()
        => static (ref TEnumerable enumerable, TElement element) => enumerable.Add(element);
}

internal class ReflectionEnumerableShape<TEnumerable>(ReflectionTypeShapeProvider provider) : IEnumerableShape<TEnumerable, object?>
    where TEnumerable : IEnumerable
{
    private readonly ReflectionTypeShapeProvider _provider = provider;

    public ITypeShape Type => _provider.GetShape<TEnumerable>();
    public ITypeShape ElementType => _provider.GetShape<object>();

    public virtual bool IsMutable => _isMutable ??= DetermineIsMutable();
    public int Rank => 1;

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

internal sealed class ReflectionListShape<TEnumerable>(ReflectionTypeShapeProvider provider) : ReflectionEnumerableShape<TEnumerable>(provider)
    where TEnumerable : IList
{
    public override bool IsMutable => true;
    public override Setter<TEnumerable, object?> GetAddElement()
        => static (ref TEnumerable enumerable, object? value) => enumerable.Add(value);
}

internal sealed class MultiDimensionalArrayShape<TEnumerable, TElement>(ReflectionTypeShapeProvider provider, int rank) : IEnumerableShape<TEnumerable, TElement>
    where TEnumerable : IEnumerable
{
    public ITypeShape Type => provider.GetShape<TEnumerable>();
    public ITypeShape ElementType => provider.GetShape<TElement>();
    public bool IsMutable => false;
    public int Rank => rank;

    public object? Accept(ITypeShapeVisitor visitor, object? state)
        => visitor.VisitEnumerable(this, state);

    public Setter<TEnumerable, TElement> GetAddElement()
        => throw new InvalidOperationException("The current enumerable shape does not support mutation.");

    public Func<TEnumerable, IEnumerable<TElement>> GetGetEnumerable()
        => static enumerable => enumerable.Cast<TElement>();
}
