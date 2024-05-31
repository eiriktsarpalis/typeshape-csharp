using System.Collections;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using TypeShape.Abstractions;
using TypeShape.SourceGenModel;

namespace TypeShape.ReflectionProvider;

[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
internal abstract class ReflectionDictionaryTypeShape<TDictionary, TKey, TValue>(ReflectionTypeShapeProvider provider) : IDictionaryShape<TDictionary, TKey, TValue>
    where TKey : notnull
{
    private CollectionConstructionStrategy? _constructionStrategy;
    private ConstructorInfo? _defaultCtor;
    private MethodInfo? _addMethod;
    private MethodBase? _enumerableCtor;
    private MethodBase? _spanCtor;
    private ConstructorInfo? _dictionaryCtor;
    private bool _isFSharpMap;

    public CollectionConstructionStrategy ConstructionStrategy => _constructionStrategy ??= DetermineConstructionStrategy();
    public ITypeShape<TKey> KeyType => provider.GetShape<TKey>();
    public ITypeShape<TValue> ValueType => provider.GetShape<TValue>();
    public ITypeShapeProvider Provider => provider;

    public abstract Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> GetGetDictionary();

    public Setter<TDictionary, KeyValuePair<TKey, TValue>> GetAddKeyValuePair()
    {
        if (ConstructionStrategy is not CollectionConstructionStrategy.Mutable)
        {
            throw new InvalidOperationException("The current dictionary shape does not support mutation.");
        }

        Debug.Assert(_addMethod != null);
        return provider.MemberAccessor.CreateDictionaryAddDelegate<TDictionary, TKey, TValue>(_addMethod);
    }

    public Func<TDictionary> GetDefaultConstructor()
    {
        if (ConstructionStrategy is not CollectionConstructionStrategy.Mutable)
        {
            throw new InvalidOperationException("The current dictionary shape does not support mutation.");
        }

        Debug.Assert(_defaultCtor != null);
        return provider.MemberAccessor.CreateDefaultConstructor<TDictionary>(new MethodConstructorShapeInfo(typeof(TDictionary), _defaultCtor, parameters: []));
    }

    public Func<IEnumerable<KeyValuePair<TKey, TValue>>, TDictionary> GetEnumerableConstructor()
    {
        if (ConstructionStrategy is not CollectionConstructionStrategy.Enumerable)
        {
            throw new InvalidOperationException("The current dictionary shape does not support enumerable constructors.");
        }

        Debug.Assert(_enumerableCtor != null);

        if (_isFSharpMap)
        {
            var mapOfSeqDelegate = ((MethodInfo)_enumerableCtor).CreateDelegate<Func<IEnumerable<Tuple<TKey, TValue>>, TDictionary>>();
            return kvps => mapOfSeqDelegate(kvps.Select(kvp => new Tuple<TKey, TValue>(kvp.Key, kvp.Value)));
        }
        
        return _enumerableCtor switch
        {
            ConstructorInfo ctorInfo => provider.MemberAccessor.CreateFuncDelegate<IEnumerable<KeyValuePair<TKey, TValue>>, TDictionary>(ctorInfo),
            _ => ((MethodInfo)_enumerableCtor).CreateDelegate<Func<IEnumerable<KeyValuePair<TKey, TValue>>, TDictionary>>(),
        };
    }

    public SpanConstructor<KeyValuePair<TKey, TValue>, TDictionary> GetSpanConstructor()
    {
        if (ConstructionStrategy is not CollectionConstructionStrategy.Span)
        {
            throw new InvalidOperationException("The current enumerable shape does not support span constructors.");
        }

        if (_dictionaryCtor is ConstructorInfo dictionaryCtor)
        {
            var dictionaryCtorDelegate = provider.MemberAccessor.CreateFuncDelegate<Dictionary<TKey, TValue>, TDictionary>(dictionaryCtor);
            return span => dictionaryCtorDelegate(CollectionHelpers.CreateDictionary(span));
        }

        Debug.Assert(_spanCtor != null);
        return _spanCtor switch
        {
            ConstructorInfo ctorInfo => provider.MemberAccessor.CreateSpanConstructorDelegate<KeyValuePair<TKey, TValue>, TDictionary>(ctorInfo),
            _ => ((MethodInfo)_spanCtor).CreateDelegate<SpanConstructor<KeyValuePair<TKey, TValue>, TDictionary>>(),
        };
    }

    private CollectionConstructionStrategy DetermineConstructionStrategy()
    {
        // TODO resolve CollectionBuilderAttribute once added for Dictionary types

        if (typeof(TDictionary).GetConstructor([]) is ConstructorInfo defaultCtor)
        {
            MethodInfo? addMethod = typeof(TDictionary).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m =>
                    m.Name is "set_Item" or "Add" &&
                    m.GetParameters() is [ParameterInfo key, ParameterInfo value] &&
                    key.ParameterType == typeof(TKey) && value.ParameterType == typeof(TValue))
                .MaxBy(m => m.Name); // Prefer set_Item over Add

            if (addMethod != null)
            {
                _defaultCtor = defaultCtor;
                _addMethod = addMethod;
                return CollectionConstructionStrategy.Mutable;
            }
        }

        if (provider.Options.UseReflectionEmit && typeof(TDictionary).GetConstructor([typeof(ReadOnlySpan<KeyValuePair<TKey, TValue>>)]) is ConstructorInfo spanCtor)
        {
            // Cannot invoke constructors with ROS parameters without Ref.Emit
            _spanCtor = spanCtor;
            return CollectionConstructionStrategy.Span;
        }

        if (typeof(TDictionary).GetConstructor([typeof(IEnumerable<KeyValuePair<TKey, TValue>>)]) is ConstructorInfo enumerableCtor)
        {
            _enumerableCtor = enumerableCtor;
            return CollectionConstructionStrategy.Enumerable;
        }

        if (typeof(TDictionary).GetConstructors()
            .FirstOrDefault(ctor => ctor.GetParameters() is [{ ParameterType: Type { IsGenericType: true } paramTy }] && paramTy.IsAssignableFrom(typeof(Dictionary<TKey, TValue>)))
            is ConstructorInfo dictionaryCtor)
        {
            // Handle types with ctors accepting IDictionary or IReadOnlyDictionary such as ReadOnlyDictionary<TKey, TValue>
            _dictionaryCtor = dictionaryCtor;
            return CollectionConstructionStrategy.Span;
        }

        if (typeof(TDictionary).IsInterface)
        {
            if (typeof(TDictionary).IsAssignableFrom(typeof(Dictionary<TKey, TValue>)))
            {
                // Handle IDictionary, IDictionary<TKey, TValue> and IReadOnlyDictionary<TKey, TValue> using Dictionary<TKey, TValue>
                MethodInfo? gm = typeof(CollectionHelpers).GetMethod(nameof(CollectionHelpers.CreateDictionary), BindingFlags.Public | BindingFlags.Static);
                _spanCtor = gm?.MakeGenericMethod(typeof(TKey), typeof(TValue));
                return _spanCtor != null ? CollectionConstructionStrategy.Span : CollectionConstructionStrategy.None;
            }

            if (typeof(TDictionary) == typeof(IDictionary))
            {
                // Handle IDictionary using Dictionary<object, object>
                Debug.Assert(typeof(TKey) == typeof(object) && typeof(TValue) == typeof(object));
                MethodInfo? gm = typeof(CollectionHelpers).GetMethod(nameof(CollectionHelpers.CreateDictionary), BindingFlags.Public | BindingFlags.Static);
                _spanCtor = gm?.MakeGenericMethod(typeof(object), typeof(object));
                return _spanCtor != null ? CollectionConstructionStrategy.Span : CollectionConstructionStrategy.None;
            }

            return CollectionConstructionStrategy.None;
        }

        if (typeof(TDictionary) == typeof(ImmutableDictionary<TKey, TValue>))
        {
            _enumerableCtor = typeof(ImmutableDictionary).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name is nameof(ImmutableDictionary.CreateRange))
                .Where(m => m.GetParameters() is [ParameterInfo p] && p.ParameterType.IsIEnumerable())
                .Select(m => m.MakeGenericMethod(typeof(TKey), typeof(TValue)))
                .FirstOrDefault();

            return _enumerableCtor != null ? CollectionConstructionStrategy.Enumerable : CollectionConstructionStrategy.None;
        }

        if (typeof(TDictionary) == typeof(ImmutableSortedDictionary<TKey, TValue>))
        {
            _enumerableCtor = typeof(ImmutableSortedDictionary).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name is nameof(ImmutableSortedDictionary.CreateRange))
                .Where(m => m.GetParameters() is [ParameterInfo p] && p.ParameterType.IsIEnumerable())
                .Select(m => m.MakeGenericMethod(typeof(TKey), typeof(TValue)))
                .FirstOrDefault();

            return _enumerableCtor != null ? CollectionConstructionStrategy.Enumerable : CollectionConstructionStrategy.None;
        }
        
        if (typeof(TDictionary) is { Name: "FSharpMap`2", Namespace: "Microsoft.FSharp.Collections" })
        {
            Type? module = typeof(TDictionary).Assembly.GetType("Microsoft.FSharp.Collections.MapModule");
            _enumerableCtor = module?.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name is "OfSeq")
                .Where(m => m.GetParameters() is [ParameterInfo p] && p.ParameterType.IsIEnumerable())
                .Select(m => m.MakeGenericMethod(typeof(TKey), typeof(TValue)))
                .FirstOrDefault();

            _isFSharpMap = _enumerableCtor != null;
            return _enumerableCtor != null ? CollectionConstructionStrategy.Enumerable : CollectionConstructionStrategy.None;
        }

        return CollectionConstructionStrategy.None;
    }
}

[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
internal sealed class ReflectionDictionaryOfTShape<TDictionary, TKey, TValue>(ReflectionTypeShapeProvider provider) : ReflectionDictionaryTypeShape<TDictionary, TKey, TValue>(provider)
    where TDictionary : IDictionary<TKey, TValue>
    where TKey : notnull
{
    public override Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> GetGetDictionary()
    {
        return static dict => CollectionHelpers.AsReadOnlyDictionary<TDictionary, TKey, TValue>(dict);
    }
}

[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
internal sealed class ReflectionReadOnlyDictionaryShape<TDictionary, TKey, TValue>(ReflectionTypeShapeProvider provider) : ReflectionDictionaryTypeShape<TDictionary, TKey, TValue>(provider)
    where TDictionary : IReadOnlyDictionary<TKey, TValue>
    where TKey : notnull
{
    public override Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> GetGetDictionary()
    {
        return static dict => dict;
    }
}

[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
internal sealed class ReflectionNonGenericDictionaryShape<TDictionary>(ReflectionTypeShapeProvider provider) : ReflectionDictionaryTypeShape<TDictionary, object, object?>(provider)
    where TDictionary : IDictionary
{
    public override Func<TDictionary, IReadOnlyDictionary<object, object?>> GetGetDictionary()
    {
        return static obj => CollectionHelpers.AsReadOnlyDictionary(obj);
    }
}