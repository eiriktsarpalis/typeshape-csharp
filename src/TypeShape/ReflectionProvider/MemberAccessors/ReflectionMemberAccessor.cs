using System.Diagnostics;
using System.Reflection;

namespace TypeShape.ReflectionProvider.MemberAccessors;

internal sealed class ReflectionMemberAccessor : IReflectionMemberAccessor
{
    public Getter<TDeclaringType, TPropertyType> CreateGetter<TDeclaringType, TPropertyType>(MemberInfo memberInfo)
    {
        Debug.Assert(memberInfo is PropertyInfo or FieldInfo);

        return memberInfo switch
        {
            PropertyInfo p => (ref TDeclaringType obj) => (TPropertyType)p.GetValue(obj)!,
            FieldInfo f => (ref TDeclaringType obj) => (TPropertyType)f.GetValue(obj)!,
            _ => default!,
        };
    }

    public Setter<TDeclaringType, TPropertyType> CreateSetter<TDeclaringType, TPropertyType>(MemberInfo memberInfo)
    {
        Debug.Assert(memberInfo is PropertyInfo or FieldInfo);

        return memberInfo switch
        {
            PropertyInfo p =>
                !typeof(TDeclaringType).IsValueType
                ? (ref TDeclaringType obj, TPropertyType value) => p.SetValue(obj, value)
                : (ref TDeclaringType obj, TPropertyType value) =>
                {
                    object? boxedObj = obj;
                    p.SetValue(boxedObj, value);
                    obj = (TDeclaringType)boxedObj!;
                }
            ,

            FieldInfo f =>
                !typeof(TDeclaringType).IsValueType
                ? (ref TDeclaringType obj, TPropertyType value) => f.SetValue(obj, value)
                : (ref TDeclaringType obj, TPropertyType value) =>
                {
                    object? boxedObj = obj;
                    f.SetValue(boxedObj, value);
                    obj = (TDeclaringType)boxedObj!;
                }
            ,
            _ => default!,
        };
    }

    public Setter<TEnumerable, TElement> CreateEnumerableAddDelegate<TEnumerable, TElement>(MethodInfo addMethod)
    {
        return !typeof(TEnumerable).IsValueType
        ? (ref TEnumerable enumerable, TElement element) => addMethod.Invoke(enumerable, new object?[] { element })
        : (ref TEnumerable enumerable, TElement element) =>
        {
            object boxed = enumerable!;
            addMethod.Invoke(boxed, new object?[] { element });
            enumerable = (TEnumerable)boxed;
        };
    }

    public Setter<TDictionary, KeyValuePair<TKey, TValue>> CreateDictionaryAddDelegate<TDictionary, TKey, TValue>(MethodInfo addMethod)
    {
        return !typeof(TDictionary).IsValueType
        ? (ref TDictionary dict, KeyValuePair<TKey, TValue> entry) => addMethod.Invoke(dict, new object?[] { entry.Key, entry.Value })
        : (ref TDictionary dict, KeyValuePair<TKey, TValue> entry) =>
        {
            object boxed = dict!;
            addMethod.Invoke(boxed, new object?[] { entry.Key, entry.Value });
            dict = (TDictionary)boxed;
        };
    }

    public Func<TDeclaringType> CreateDefaultConstructor<TDeclaringType>(ConstructorInfo? ctorInfo)
    {
        Debug.Assert(ctorInfo != null || typeof(TDeclaringType).IsValueType);
        return ctorInfo != null
            ? () => (TDeclaringType)ctorInfo.Invoke(null)!
            : static () => default(TDeclaringType)!;
    }

    public Type CreateConstructorArgumentStateType(ParameterInfo[] _) => typeof(object?[]);

    public Func<TArgumentState> CreateConstructorArgumentStateCtor<TArgumentState>(ParameterInfo[] parameters)
    {
        Debug.Assert(typeof(TArgumentState) == typeof(object?[]));
        return (Func<TArgumentState>)(object)CreateConstructorArgumentArrayFunc(parameters);
    }

    public Setter<TArgumentState, TParameter> CreateConstructorArgumentStateSetter<TArgumentState, TParameter>(int parameterIndex, int totalParameters)
    {
        Debug.Assert(typeof(TArgumentState) == typeof(object?[]));
        return (Setter<TArgumentState, TParameter>)(object)new Setter<object?[], TParameter>((ref object?[] state, TParameter value) => state[parameterIndex] = value);
    }

    public Func<TArgumentState, TDeclaringType> CreateParameterizedConstructor<TArgumentState, TDeclaringType>(ConstructorInfo? ctorInfo, ParameterInfo[] parameterInfos)
    {
        Debug.Assert(typeof(TArgumentState) == typeof(object?[]));
        Debug.Assert(ctorInfo != null || typeof(TDeclaringType).IsValueType && parameterInfos.Length == 0);

        return ctorInfo is null
            ? static _ => default!
            : (Func<TArgumentState, TDeclaringType>)(object)new Func<object?[], TDeclaringType>(state => (TDeclaringType)ctorInfo.Invoke(state)!);
    }

    internal static Func<object?[]> CreateConstructorArgumentArrayFunc(ParameterInfo[] parameters)
    {
        int arity = parameters.Length;
        if (arity == 0)
        {
            return static () => Array.Empty<object?>();
        }
        else if (parameters.Any(param => param.HasDefaultValue))
        {
            object?[] sourceParamArray = parameters.Select(p => p.GetDefaultValueNormalized()).ToArray();
            return () => (object?[])sourceParamArray.Clone();
        }
        else
        {
            return () => new object?[arity];
        }
    }
}
