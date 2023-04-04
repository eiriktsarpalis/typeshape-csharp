using System.Collections;
using System.Diagnostics;
using System.Reflection;

namespace TypeShape.ReflectionProvider;

internal sealed class ReflectionGenericDictionaryShape<TDictionary, TKey, TValue> : IDictionaryShape<TDictionary, TKey, TValue>
    where TDictionary : IDictionary<TKey, TValue>
    where TKey : notnull
{
    private readonly ReflectionTypeShapeProvider _provider;
    
    public ReflectionGenericDictionaryShape(ReflectionTypeShapeProvider provider)
        => _provider = provider;

    public ITypeShape Type => _provider.GetShape<TDictionary>();

    public ITypeShape KeyType => _provider.GetShape<TKey>();

    public ITypeShape ValueType => _provider.GetShape<TValue>();

    public bool IsMutable => true;

    public object? Accept(ITypeShapeVisitor visitor, object? state)
        => visitor.VisitDictionary(this, state);

    public Setter<TDictionary, KeyValuePair<TKey, TValue>> GetAddKeyValuePair()
    {
        return static (ref TDictionary dict, KeyValuePair<TKey, TValue> kvp) => dict[kvp.Key] = kvp.Value;
    }

    public Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> GetGetDictionary()
    {
        return static dict => SourceGenModel.CollectionHelpers.AsReadOnlyDictionary<TDictionary, TKey, TValue>(dict);
    }
}

internal sealed class ReflectionReadOnlyDictionaryShape<TDictionary, TKey, TValue> : IDictionaryShape<TDictionary, TKey, TValue>
    where TDictionary : IReadOnlyDictionary<TKey, TValue>
    where TKey : notnull
{
    private readonly ReflectionTypeShapeProvider _provider;

    public ReflectionReadOnlyDictionaryShape(ReflectionTypeShapeProvider provider)
        => _provider = provider;

    public ITypeShape Type => _provider.GetShape<TDictionary>();

    public ITypeShape KeyType => _provider.GetShape<TKey>();

    public ITypeShape ValueType => _provider.GetShape<TValue>();

    public bool IsMutable => _isMutable ??= DetermineIsMutable();
    private MethodInfo? _addMethod;
    private bool? _isMutable;

    private bool DetermineIsMutable()
    {
        foreach (MethodInfo methodInfo in typeof(TDictionary).GetMethods(BindingFlags.Public | BindingFlags.Instance))
        {
            if (methodInfo.Name is "set_Item" or "Add" &&
                methodInfo.ReturnType == typeof(void) &&
                methodInfo.GetParameters() is [ParameterInfo p1, ParameterInfo p2] &&
                p1.ParameterType == typeof(TKey) && p2.ParameterType == typeof(TValue))
            {
                _addMethod = methodInfo;
                return true;
            }
        }

        return false;
    }

    public object? Accept(ITypeShapeVisitor visitor, object? state)
        => visitor.VisitDictionary(this, state);

    public Setter<TDictionary, KeyValuePair<TKey, TValue>> GetAddKeyValuePair()
    {
        if (!IsMutable)
        {
            throw new InvalidOperationException("The current dictionary shape does not support mutation.");
        }

        Debug.Assert(_addMethod != null);
        return _provider.MemberAccessor.CreateDictionaryAddDelegate<TDictionary, TKey, TValue>(_addMethod);
    }

    public Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> GetGetDictionary()
    {
        return static dict => dict;
    }
}

internal sealed class ReflectionDictionaryShape<TDictionary> : IDictionaryShape<TDictionary, object, object?>
    where TDictionary : IDictionary
{
    private readonly ReflectionTypeShapeProvider _provider;

    public ReflectionDictionaryShape(ReflectionTypeShapeProvider provider)
        => _provider = provider;

    public ITypeShape Type => _provider.GetShape<TDictionary>();

    public ITypeShape KeyType => _provider.GetShape<object>();

    public ITypeShape ValueType => _provider.GetShape<object>();

    public bool IsMutable => true;

    public object? Accept(ITypeShapeVisitor visitor, object? state)
        => visitor.VisitDictionary(this, state);

    public Setter<TDictionary, KeyValuePair<object, object?>> GetAddKeyValuePair()
    {
        return static (ref TDictionary dict, KeyValuePair<object, object?> kvp) => dict[kvp.Key] = kvp.Value;
    }

    public Func<TDictionary, IReadOnlyDictionary<object, object?>> GetGetDictionary()
        => static obj => SourceGenModel.CollectionHelpers.AsReadOnlyDictionary(obj);
}