using PolyType.Abstractions;
using PolyType.SourceGenModel;
using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices;

namespace PolyType.ReflectionProvider;

[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
internal abstract class ReflectionEnumerableTypeShape<TEnumerable, TElement>(ReflectionTypeShapeProvider provider) : ReflectionTypeShape<TEnumerable>(provider), IEnumerableTypeShape<TEnumerable, TElement>
{
    private CollectionConstructionStrategy? _constructionStrategy;
    private ConstructorInfo? _defaultCtor;
    private MethodInfo? _addMethod;
    private MethodBase? _enumerableCtor;
    private MethodBase? _spanCtor;
    private ConstructorInfo? _listCtor;

    public virtual CollectionConstructionStrategy ConstructionStrategy => _constructionStrategy ??= DetermineConstructionStrategy();
    public virtual int Rank => 1;
    public abstract Func<TEnumerable, IEnumerable<TElement>> GetGetEnumerable();

    public sealed override TypeShapeKind Kind => TypeShapeKind.Enumerable;
    public sealed override object? Accept(ITypeShapeVisitor visitor, object? state = null) => visitor.VisitEnumerable(this, state);
    public ITypeShape<TElement> ElementType => Provider.GetShape<TElement>();
    ITypeShape IEnumerableTypeShape.ElementType => ElementType;

    public virtual Setter<TEnumerable, TElement> GetAddElement()
    {
        if (ConstructionStrategy is not CollectionConstructionStrategy.Mutable)
        {
            throw new InvalidOperationException("The current enumerable shape does not support mutation.");
        }

        DebugExt.Assert(_addMethod != null);
        return Provider.MemberAccessor.CreateEnumerableAddDelegate<TEnumerable, TElement>(_addMethod);
    }

    public virtual Func<TEnumerable> GetDefaultConstructor()
    {
        if (ConstructionStrategy is not CollectionConstructionStrategy.Mutable)
        {
            throw new InvalidOperationException("The current enumerable shape does not support default constructors.");
        }

        Debug.Assert(_defaultCtor != null);
        return Provider.MemberAccessor.CreateDefaultConstructor<TEnumerable>(new MethodConstructorShapeInfo(typeof(TEnumerable), _defaultCtor, parameters: []));
    }

    public virtual Func<IEnumerable<TElement>, TEnumerable> GetEnumerableConstructor()
    {
        if (ConstructionStrategy is not CollectionConstructionStrategy.Enumerable)
        {
            throw new InvalidOperationException("The current enumerable shape does not support enumerable constructors.");
        }

        DebugExt.Assert(_enumerableCtor != null);
        return _enumerableCtor switch
        {
            ConstructorInfo ctorInfo => Provider.MemberAccessor.CreateFuncDelegate<IEnumerable<TElement>, TEnumerable>(ctorInfo),
            _ => ((MethodInfo)_enumerableCtor).CreateDelegate<Func<IEnumerable<TElement>, TEnumerable>>(),
        };
    }

    public virtual SpanConstructor<TElement, TEnumerable> GetSpanConstructor()
    {
        if (ConstructionStrategy is not CollectionConstructionStrategy.Span)
        {
            throw new InvalidOperationException("The current enumerable shape does not support span constructors.");
        }

        if (_listCtor is ConstructorInfo listCtor)
        {
            var listCtorDelegate = Provider.MemberAccessor.CreateFuncDelegate<List<TElement>, TEnumerable>(listCtor);
            return span => listCtorDelegate(CollectionHelpers.CreateList(span));
        }

        DebugExt.Assert(_spanCtor != null);
        return _spanCtor switch
        {
            ConstructorInfo ctorInfo => Provider.MemberAccessor.CreateSpanConstructorDelegate<TElement, TEnumerable>(ctorInfo),
            _ => ((MethodInfo)_spanCtor).CreateDelegate<SpanConstructor<TElement, TEnumerable>>(),
        };
    }

    private CollectionConstructionStrategy DetermineConstructionStrategy()
    {
        if (typeof(TEnumerable).TryGetCollectionBuilderAttribute(typeof(TElement), out MethodInfo? builderMethod))
        {
            _spanCtor = builderMethod;
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

        if (Provider.Options.UseReflectionEmit && typeof(TEnumerable).GetConstructor([typeof(ReadOnlySpan<TElement>)]) is ConstructorInfo spanCtor)
        {
            // Cannot invoke constructors with ROS parameters without Ref.Emit
            _spanCtor = spanCtor;
            return CollectionConstructionStrategy.Span;
        }

        if (typeof(TEnumerable).GetConstructor([typeof(IEnumerable<TElement>)]) is ConstructorInfo enumerableCtor)
        {
            _enumerableCtor = enumerableCtor;
            return CollectionConstructionStrategy.Enumerable;
        }

        if (typeof(TEnumerable).GetConstructors()
                .FirstOrDefault(ctor => ctor.GetParameters() is [{ ParameterType: { IsGenericType: true } paramTy }] && paramTy.IsAssignableFrom(typeof(List<TElement>)))
                is ConstructorInfo listCtor)
        {
            // Handle types accepting IList<T> or IReadOnlyList<T> such as ReadOnlyCollection<T>
            _listCtor = listCtor;
            return CollectionConstructionStrategy.Span;
        }

        if (typeof(TEnumerable).IsInterface)
        {
            if (typeof(TEnumerable).IsAssignableFrom(typeof(List<TElement>)))
            {
                // Handle IEnumerable<T>, ICollection<T>, IList<T>, IReadOnlyCollection<T> and IReadOnlyList<T> types using List<T>
                MethodInfo? gm = typeof(CollectionHelpers).GetMethod(nameof(CollectionHelpers.CreateList), BindingFlags.Public | BindingFlags.Static);
                _spanCtor = gm?.MakeGenericMethod(typeof(TElement));
                return _spanCtor != null ? CollectionConstructionStrategy.Span : CollectionConstructionStrategy.None;
            }

            if (typeof(TEnumerable).IsAssignableFrom(typeof(HashSet<TElement>)))
            {
                // Handle ISet<T> and IReadOnlySet<T> types using HashSet<T>
                MethodInfo? gm = typeof(CollectionHelpers).GetMethod(nameof(CollectionHelpers.CreateHashSet), BindingFlags.Public | BindingFlags.Static);
                _spanCtor = gm?.MakeGenericMethod(typeof(TElement));
                return _spanCtor != null ? CollectionConstructionStrategy.Span : CollectionConstructionStrategy.None;
            }

            if (typeof(TEnumerable).IsAssignableFrom(typeof(IList)))
            {
                // Handle IList, ICollection and IEnumerable interfaces using List<object?>
                MethodInfo? gm = typeof(CollectionHelpers).GetMethod(nameof(CollectionHelpers.CreateList), BindingFlags.Public | BindingFlags.Static);
                _spanCtor = gm?.MakeGenericMethod(typeof(object));
                return _spanCtor != null ? CollectionConstructionStrategy.Span : CollectionConstructionStrategy.None;
            }
        }

        if (typeof(TEnumerable) is { Name: "ImmutableArray`1", Namespace: "System.Collections.Immutable" })
        {
            Type? factoryType = typeof(TEnumerable).Assembly.GetType("System.Collections.Immutable.ImmutableArray");
            _enumerableCtor = factoryType?.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name is "CreateRange")
                .Where(m => m.GetParameters() is [ParameterInfo p] && p.ParameterType.IsIEnumerable())
                .Select(m => m.MakeGenericMethod(typeof(TElement)))
                .FirstOrDefault();

            return _enumerableCtor != null ? CollectionConstructionStrategy.Enumerable : CollectionConstructionStrategy.None;
        }

        if (typeof(TEnumerable) is { Name: "ImmutableList`1", Namespace: "System.Collections.Immutable" })
        {
            Type? factoryType = typeof(TEnumerable).Assembly.GetType("System.Collections.Immutable.ImmutableList");
            _enumerableCtor = factoryType?.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name is "CreateRange")
                .Where(m => m.GetParameters() is [ParameterInfo p] && p.ParameterType.IsIEnumerable())
                .Select(m => m.MakeGenericMethod(typeof(TElement)))
                .FirstOrDefault();

            return _enumerableCtor != null ? CollectionConstructionStrategy.Enumerable : CollectionConstructionStrategy.None;
        }

        if (typeof(TEnumerable) is { Name: "ImmutableQueue`1", Namespace: "System.Collections.Immutable" })
        {
            Type? factoryType = typeof(TEnumerable).Assembly.GetType("System.Collections.Immutable.ImmutableQueue");
            _enumerableCtor = factoryType?.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name is "CreateRange")
                .Where(m => m.GetParameters() is [ParameterInfo p] && p.ParameterType.IsIEnumerable())
                .Select(m => m.MakeGenericMethod(typeof(TElement)))
                .FirstOrDefault();

            return _enumerableCtor != null ? CollectionConstructionStrategy.Enumerable : CollectionConstructionStrategy.None;
        }

        if (typeof(TEnumerable) is { Name: "ImmutableStack`1", Namespace: "System.Collections.Immutable" })
        {
            Type? factoryType = typeof(TEnumerable).Assembly.GetType("System.Collections.Immutable.ImmutableStack");
            _enumerableCtor = factoryType?.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name is "CreateRange")
                .Where(m => m.GetParameters() is [ParameterInfo p] && p.ParameterType.IsIEnumerable())
                .Select(m => m.MakeGenericMethod(typeof(TElement)))
                .FirstOrDefault();

            return _enumerableCtor != null ? CollectionConstructionStrategy.Enumerable : CollectionConstructionStrategy.None;
        }

        if (typeof(TEnumerable) is { Name: "ImmutableHashSet`1", Namespace: "System.Collections.Immutable" })
        {
            Type? factoryType = typeof(TEnumerable).Assembly.GetType("System.Collections.Immutable.ImmutableHashSet");
            _enumerableCtor = factoryType?.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name is "CreateRange")
                .Where(m => m.GetParameters() is [ParameterInfo p] && p.ParameterType.IsIEnumerable())
                .Select(m => m.MakeGenericMethod(typeof(TElement)))
                .FirstOrDefault();

            return _enumerableCtor != null ? CollectionConstructionStrategy.Enumerable : CollectionConstructionStrategy.None;
        }

        if (typeof(TEnumerable) is { Name: "ImmutableSortedSet`1", Namespace: "System.Collections.Immutable" })
        {
            Type? factoryType = typeof(TEnumerable).Assembly.GetType("System.Collections.Immutable.ImmutableSortedSet");
            _enumerableCtor = factoryType?.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name is "CreateRange")
                .Where(m => m.GetParameters() is [ParameterInfo p] && p.ParameterType.IsIEnumerable())
                .Select(m => m.MakeGenericMethod(typeof(TElement)))
                .FirstOrDefault();

            return _enumerableCtor != null ? CollectionConstructionStrategy.Enumerable : CollectionConstructionStrategy.None;
        }

        if (typeof(TEnumerable) is { Name: "FSharpList`1", Namespace: "Microsoft.FSharp.Collections" })
        {
            Type? module = typeof(TEnumerable).Assembly.GetType("Microsoft.FSharp.Collections.ListModule");
            _enumerableCtor = module?.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name is "OfSeq")
                .Where(m => m.GetParameters() is [ParameterInfo p] && p.ParameterType.IsIEnumerable())
                .Select(m => m.MakeGenericMethod(typeof(TElement)))
                .FirstOrDefault();

            return _enumerableCtor != null ? CollectionConstructionStrategy.Enumerable : CollectionConstructionStrategy.None;
        }

        return CollectionConstructionStrategy.None;
    }
}

[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
internal sealed class ReflectionEnumerableTypeOfTShape<TEnumerable, TElement>(ReflectionTypeShapeProvider provider)
    : ReflectionEnumerableTypeShape<TEnumerable, TElement>(provider)
    where TEnumerable : IEnumerable<TElement>
{
    public override Func<TEnumerable, IEnumerable<TElement>> GetGetEnumerable()
        => static enumerable => enumerable;
}

[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
internal sealed class ReflectionNonGenericEnumerableTypeShape<TEnumerable>(ReflectionTypeShapeProvider provider)
    : ReflectionEnumerableTypeShape<TEnumerable, object?>(provider)
    where TEnumerable : IEnumerable
{
    public override Func<TEnumerable, IEnumerable<object?>> GetGetEnumerable()
        => static enumerable => enumerable.Cast<object?>();
}

[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
internal sealed class ReflectionArrayTypeShape<TElement>(ReflectionTypeShapeProvider provider)
    : ReflectionEnumerableTypeShape<TElement[], TElement>(provider)
{
    public override CollectionConstructionStrategy ConstructionStrategy => CollectionConstructionStrategy.Span;
    public override Func<TElement[], IEnumerable<TElement>> GetGetEnumerable() => static array => array;
    public override SpanConstructor<TElement, TElement[]> GetSpanConstructor() => static span => span.ToArray();
}

[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
internal sealed class MultiDimensionalArrayTypeShape<TEnumerable, TElement>(ReflectionTypeShapeProvider provider, int rank)
    : ReflectionEnumerableTypeShape<TEnumerable, TElement>(provider)
    where TEnumerable : IEnumerable
{
    public override CollectionConstructionStrategy ConstructionStrategy => CollectionConstructionStrategy.None;
    public override int Rank => rank;
    public override Func<TEnumerable, IEnumerable<TElement>> GetGetEnumerable()
        => static enumerable => enumerable.Cast<TElement>();
}

[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
internal sealed class ReadOnlyMemoryTypeShape<TElement>(ReflectionTypeShapeProvider provider)
    : ReflectionEnumerableTypeShape<ReadOnlyMemory<TElement>, TElement>(provider)
{
    public override CollectionConstructionStrategy ConstructionStrategy => CollectionConstructionStrategy.Span;
    public override Func<ReadOnlyMemory<TElement>, IEnumerable<TElement>> GetGetEnumerable() => static memory => MemoryMarshal.ToEnumerable(memory);
    public override SpanConstructor<TElement, ReadOnlyMemory<TElement>> GetSpanConstructor() => static span => span.ToArray();
}

[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
internal sealed class MemoryTypeShape<TElement>(ReflectionTypeShapeProvider provider)
    : ReflectionEnumerableTypeShape<Memory<TElement>, TElement>(provider)
{
    public override CollectionConstructionStrategy ConstructionStrategy => CollectionConstructionStrategy.Span;
    public override Func<Memory<TElement>, IEnumerable<TElement>> GetGetEnumerable() => static memory => MemoryMarshal.ToEnumerable((ReadOnlyMemory<TElement>)memory);
    public override SpanConstructor<TElement, Memory<TElement>> GetSpanConstructor() => static span => span.ToArray();
}