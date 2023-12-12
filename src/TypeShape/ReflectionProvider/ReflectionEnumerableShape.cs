using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices;
using TypeShape.SourceGenModel;

namespace TypeShape.ReflectionProvider;

[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
internal abstract class ReflectionEnumerableShape<TEnumerable, TElement>(ReflectionTypeShapeProvider provider) 
    : IEnumerableShape<TEnumerable, TElement>
{
    private CollectionConstructionStrategy? _constructionStrategy;
    private ConstructorInfo? _defaultCtor;
    private MethodInfo? _addMethod;
    private ConstructorInfo? _enumerableCtor;
    private MethodInfo? _spanFactory;

    public virtual CollectionConstructionStrategy ConstructionStrategy => _constructionStrategy ??= DetermineConstructionStrategy();
    public virtual int Rank => 1;

    public ITypeShape<TEnumerable> Type => provider.GetShape<TEnumerable>();
    public ITypeShape<TElement> ElementType => provider.GetShape<TElement>();

    public abstract Func<TEnumerable, IEnumerable<TElement>> GetGetEnumerable();

    public virtual Setter<TEnumerable, TElement> GetAddElement()
    {
        if (ConstructionStrategy is not CollectionConstructionStrategy.Mutable)
        {
            throw new InvalidOperationException("The current enumerable shape does not support mutation.");
        }

        Debug.Assert(_addMethod != null);
        return provider.MemberAccessor.CreateEnumerableAddDelegate<TEnumerable, TElement>(_addMethod);
    }

    public virtual Func<TEnumerable> GetDefaultConstructor()
    {
        if (ConstructionStrategy is not CollectionConstructionStrategy.Mutable)
        {
            throw new InvalidOperationException("The current enumerable shape does not support default constructors.");
        }

        Debug.Assert(_defaultCtor != null);
        return provider.MemberAccessor.CreateDefaultConstructor<TEnumerable>(new MethodConstructorShapeInfo(typeof(TEnumerable), _defaultCtor));
    }

    public virtual Func<IEnumerable<TElement>, TEnumerable> GetEnumerableConstructor()
    {
        if (ConstructionStrategy is not CollectionConstructionStrategy.Enumerable)
        {
            throw new InvalidOperationException("The current enumerable shape does not support enumerable constructors.");
        }

        Debug.Assert(_enumerableCtor != null);
        return provider.MemberAccessor.CreateDelegate<IEnumerable<TElement>, TEnumerable>(_enumerableCtor);
    }

    public virtual SpanConstructor<TElement, TEnumerable> GetSpanConstructor()
    {
        if (ConstructionStrategy is not CollectionConstructionStrategy.Span)
        {
            throw new InvalidOperationException("The current enumerable shape does not support span constructors.");
        }

        Debug.Assert(_spanFactory != null);
        return _spanFactory.CreateDelegate<SpanConstructor<TElement, TEnumerable>>();
    }

    private CollectionConstructionStrategy DetermineConstructionStrategy()
    {
        if (typeof(TEnumerable).TryGetCollectionBuilderAttribute(typeof(TElement), out MethodInfo? builderMethod))
        {
            _spanFactory = builderMethod;
            return CollectionConstructionStrategy.Span;
        }

        if (typeof(TEnumerable).GetConstructor([]) is ConstructorInfo defaultCtor)
        {
            foreach (MethodInfo methodInfo in typeof(TEnumerable).GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                if (methodInfo.Name is "Add" or "Enqueue" or "Push" &&
                    methodInfo.GetParameters() is [ParameterInfo parameter] &&
                    parameter.ParameterType == typeof(TElement))
                {
                    _defaultCtor = defaultCtor;
                    _addMethod = methodInfo;
                    return CollectionConstructionStrategy.Mutable;
                }
            }
        }

        if (typeof(TEnumerable).GetConstructor([typeof(IEnumerable<TElement>)]) is ConstructorInfo enumerableCtor)
        {
            _enumerableCtor = enumerableCtor;
            return CollectionConstructionStrategy.Enumerable;
        }

        if (typeof(TEnumerable).IsInterface)
        {
            if (typeof(TEnumerable).IsAssignableFrom(typeof(List<TElement>)))
            {
                // Handle IEnumerable<T>, ICollection<T>, IList<T>, IReadOnlyCollection<T> and IReadOnlyList<T> types using List<T>
                MethodInfo? gm = typeof(CollectionHelpers).GetMethod(nameof(CollectionHelpers.CreateList), BindingFlags.Public | BindingFlags.Static);
                _spanFactory = gm?.MakeGenericMethod(typeof(TElement));
                return _spanFactory != null ? CollectionConstructionStrategy.Span : CollectionConstructionStrategy.None;
            }

            if (typeof(TEnumerable).IsAssignableFrom(typeof(HashSet<TElement>)))
            {
                // Handle ISet<T> and IReadOnlySet<T> types using HashSet<T>
                MethodInfo? gm = typeof(CollectionHelpers).GetMethod(nameof(CollectionHelpers.CreateHashSet), BindingFlags.Public | BindingFlags.Static);
                _spanFactory = gm?.MakeGenericMethod(typeof(TElement));
                return _spanFactory != null ? CollectionConstructionStrategy.Span : CollectionConstructionStrategy.None;
            }

            if (typeof(TEnumerable).IsAssignableFrom(typeof(IList)))
            {
                // Handle IList, ICollection and IEnumerable interfaces using List<object?>
                MethodInfo? gm = typeof(CollectionHelpers).GetMethod(nameof(CollectionHelpers.CreateList), BindingFlags.Public | BindingFlags.Static);
                _spanFactory = gm?.MakeGenericMethod(typeof(object));
                return _spanFactory != null ? CollectionConstructionStrategy.Span : CollectionConstructionStrategy.None;
            }
        }

        return CollectionConstructionStrategy.None;
    }
}

[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
internal sealed class ReflectionEnumerableOfTShape<TEnumerable, TElement>(ReflectionTypeShapeProvider provider) 
    : ReflectionEnumerableShape<TEnumerable, TElement>(provider)
    where TEnumerable : IEnumerable<TElement>
{
    public override Func<TEnumerable, IEnumerable<TElement>> GetGetEnumerable()
        => static enumerable => enumerable;
}

[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
internal sealed class ReflectionNonGenericEnumerableShape<TEnumerable>(ReflectionTypeShapeProvider provider) 
    : ReflectionEnumerableShape<TEnumerable, object?>(provider)
    where TEnumerable : IEnumerable
{
    public override Func<TEnumerable, IEnumerable<object?>> GetGetEnumerable()
        => static enumerable => enumerable.Cast<object?>();
}

[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
internal sealed class ReflectionArrayShape<TElement>(ReflectionTypeShapeProvider provider) 
    : ReflectionEnumerableShape<TElement[], TElement>(provider)
{
    public override CollectionConstructionStrategy ConstructionStrategy => CollectionConstructionStrategy.Span;
    public override Func<TElement[], IEnumerable<TElement>> GetGetEnumerable() => static array => array;
    public override SpanConstructor<TElement, TElement[]> GetSpanConstructor() => static span => span.ToArray();
}

[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
internal sealed class MultiDimensionalArrayShape<TEnumerable, TElement>(ReflectionTypeShapeProvider provider, int rank) 
    : ReflectionEnumerableShape<TEnumerable, TElement>(provider)
    where TEnumerable : IEnumerable
{
    public override CollectionConstructionStrategy ConstructionStrategy => CollectionConstructionStrategy.None;
    public override int Rank => rank;
    public override Func<TEnumerable, IEnumerable<TElement>> GetGetEnumerable()
        => static enumerable => enumerable.Cast<TElement>();
}

[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
internal sealed class ReadOnlyMemoryShape<TElement>(ReflectionTypeShapeProvider provider) 
    : ReflectionEnumerableShape<ReadOnlyMemory<TElement>, TElement>(provider)
{
    public override CollectionConstructionStrategy ConstructionStrategy => CollectionConstructionStrategy.Span;
    public override Func<ReadOnlyMemory<TElement>, IEnumerable<TElement>> GetGetEnumerable() => static memory => MemoryMarshal.ToEnumerable(memory);
    public override SpanConstructor<TElement, ReadOnlyMemory<TElement>> GetSpanConstructor() => static span => span.ToArray();
}

[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
internal sealed class MemoryShape<TElement>(ReflectionTypeShapeProvider provider) 
    : ReflectionEnumerableShape<Memory<TElement>, TElement>(provider)
{
    public override CollectionConstructionStrategy ConstructionStrategy => CollectionConstructionStrategy.Span;
    public override Func<Memory<TElement>, IEnumerable<TElement>> GetGetEnumerable() => static memory => MemoryMarshal.ToEnumerable((ReadOnlyMemory<TElement>)memory);
    public override SpanConstructor<TElement, Memory<TElement>> GetSpanConstructor() => static span => span.ToArray();
}