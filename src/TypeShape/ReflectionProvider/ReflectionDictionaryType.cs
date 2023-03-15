using System.Collections;
using System.Diagnostics;
using System.Reflection;

namespace TypeShape.ReflectionProvider;

internal sealed class ReflectionGenericDictionaryType<TDictionary, TKey, TValue> : IDictionaryType<TDictionary, TKey, TValue>
    where TDictionary : IDictionary<TKey, TValue>
    where TKey : notnull
{
    private readonly ReflectionTypeShapeProvider _provider;
    
    public ReflectionGenericDictionaryType(ReflectionTypeShapeProvider provider)
        => _provider = provider;

    public IType Type => _provider.GetShape(typeof(TDictionary));

    public IType KeyType => _provider.GetShape(typeof(TKey));

    public IType ValueType => _provider.GetShape(typeof(TValue));

    public bool IsMutable => true;

    public object? Accept(IDictionaryTypeVisitor visitor, object? state)
        => visitor.VisitDictionaryType(this, state);

    public Setter<TDictionary, KeyValuePair<TKey, TValue>> GetAddKeyValuePair()
    {
        return static (ref TDictionary dict, KeyValuePair<TKey, TValue> kvp) => dict[kvp.Key] = kvp.Value;
    }

    public Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> GetGetDictionary()
    {
        return static dict => SourceGenModel.CollectionHelpers.AsReadOnlyDictionary<TDictionary, TKey, TValue>(dict);
    }
}

internal sealed class ReflectionReadOnlyDictionaryType<TDictionary, TKey, TValue> : IDictionaryType<TDictionary, TKey, TValue>
    where TDictionary : IReadOnlyDictionary<TKey, TValue>
    where TKey : notnull
{
    private readonly ReflectionTypeShapeProvider _provider;

    public ReflectionReadOnlyDictionaryType(ReflectionTypeShapeProvider provider)
        => _provider = provider;

    public IType Type => _provider.GetShape(typeof(TDictionary));

    public IType KeyType => _provider.GetShape(typeof(TKey));

    public IType ValueType => _provider.GetShape(typeof(TValue));

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

    public object? Accept(IDictionaryTypeVisitor visitor, object? state)
        => visitor.VisitDictionaryType(this, state);

    public Setter<TDictionary, KeyValuePair<TKey, TValue>> GetAddKeyValuePair()
    {
        if (!IsMutable)
        {
            throw new InvalidOperationException();
        }

        Debug.Assert(_addMethod != null);
        return _provider.MemberAccessor.CreateDictionaryAddDelegate<TDictionary, TKey, TValue>(_addMethod);
    }

    public Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> GetGetDictionary()
    {
        return static dict => dict;
    }
}

internal sealed class ReflectionDictionaryType<TDictionary> : IDictionaryType<TDictionary, object, object?>
    where TDictionary : IDictionary
{
    private readonly ReflectionTypeShapeProvider _provider;

    public ReflectionDictionaryType(ReflectionTypeShapeProvider provider)
        => _provider = provider;

    public IType Type => _provider.GetShape(typeof(TDictionary));

    public IType KeyType => _provider.GetShape(typeof(object));

    public IType ValueType => _provider.GetShape(typeof(object));

    public bool IsMutable => true;

    public object? Accept(IDictionaryTypeVisitor visitor, object? state)
        => visitor.VisitDictionaryType(this, state);

    public Setter<TDictionary, KeyValuePair<object, object?>> GetAddKeyValuePair()
    {
        return static (ref TDictionary dict, KeyValuePair<object, object?> kvp) => dict[kvp.Key] = kvp.Value;
    }

    public Func<TDictionary, IReadOnlyDictionary<object, object?>> GetGetDictionary()
        => static obj => SourceGenModel.CollectionHelpers.AsReadOnlyDictionary(obj);
}