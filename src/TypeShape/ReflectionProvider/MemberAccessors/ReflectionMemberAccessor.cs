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


    public Func<TDeclaringType> CreateDefaultConstructor<TDeclaringType>(ConstructorShapeInfo shapeInfo)
    {
        Debug.Assert(shapeInfo.TotalParameters == 0);
        Debug.Assert(shapeInfo.ConstructorInfo != null || typeof(TDeclaringType).IsValueType);
        return shapeInfo.ConstructorInfo is { } ctorInfo
            ? () => (TDeclaringType)ctorInfo.Invoke(null)!
            : static () => default(TDeclaringType)!;
    }

    public Type CreateConstructorArgumentStateType(ConstructorShapeInfo shapeInfo)
        => shapeInfo.MemberInitializers.Length == 0 ? typeof(object?[]) : typeof((object?[] ctorArgs, object?[] memberInitializerArgs));

    public Func<TArgumentState> CreateConstructorArgumentStateCtor<TArgumentState>(ConstructorShapeInfo shapeInfo)
    {
        if (shapeInfo.MemberInitializers.Length == 0)
        {
            Debug.Assert(typeof(TArgumentState) == typeof(object?[]));
            return (Func<TArgumentState>)(object)CreateConstructorArgumentArrayFunc(shapeInfo);
        }
        else
        {
            Debug.Assert(typeof(TArgumentState) == typeof((object?[], object?[])));
            return (Func<TArgumentState>)(object)CreateConstructorAndMemberInitializerArgumentArrayFunc(shapeInfo);
        }
    }

    public Setter<TArgumentState, TParameter> CreateConstructorArgumentStateSetter<TArgumentState, TParameter>(ConstructorShapeInfo shapeInfo, int parameterIndex)
    {
        if (shapeInfo.MemberInitializers.Length == 0)
        {
            Debug.Assert(typeof(TArgumentState) == typeof(object?[]));
            return (Setter<TArgumentState, TParameter>)(object)new Setter<object?[], TParameter>((ref object?[] state, TParameter value) => state[parameterIndex] = value);
        }

        Debug.Assert(typeof(TArgumentState) == typeof((object?[], object?[])));
        if (parameterIndex < shapeInfo.Parameters.Length)
        {
            return (Setter<TArgumentState, TParameter>)(object)new Setter<(object?[], object?[]), TParameter>((ref (object?[] ctorArgs, object?[]) state, TParameter value) => state.ctorArgs[parameterIndex] = value);
        }
        else
        {
            int initializerIndex = parameterIndex - shapeInfo.Parameters.Length;
            return (Setter<TArgumentState, TParameter>)(object)new Setter<(object?[], object?[]), TParameter>((ref (object?[], object?[] memberArgs) state, TParameter value) => state.memberArgs[initializerIndex] = value);
        }
    }

    public Func<TArgumentState, TDeclaringType> CreateParameterizedConstructor<TArgumentState, TDeclaringType>(ConstructorShapeInfo shapeInfo)
    {
        Debug.Assert(shapeInfo.ConstructorInfo != null || typeof(TDeclaringType).IsValueType && shapeInfo.Parameters.Length == 0);
        MemberInitializerInfo[] memberInitializers = shapeInfo.MemberInitializers;

        if (memberInitializers.Length == 0)
        {
            Debug.Assert(typeof(TArgumentState) == typeof(object?[]));
            return shapeInfo.ConstructorInfo is { } ctorInfo
                ? (Func<TArgumentState, TDeclaringType>)(object)new Func<object?[], TDeclaringType>(state => (TDeclaringType)ctorInfo.Invoke(state)!)
                : static _ => default!;
        }
        else
        {
            Debug.Assert(typeof(TArgumentState) == typeof((object?[], object?[])));

            if (shapeInfo.ConstructorInfo is { } ctorInfo)
            {
                return (Func<TArgumentState, TDeclaringType>)(object)new Func<(object?[] ctorArgs, object?[] memberArgs), TDeclaringType>(state =>
                {
                    object obj = ctorInfo.Invoke(state.ctorArgs);
                    PopulateMemberInitializers(obj, memberInitializers, state.memberArgs);
                    return (TDeclaringType)obj!;
                });
            }
            else
            {
                return (Func<TArgumentState, TDeclaringType>)(object)new Func<(object?[] ctorArgs, object?[] memberArgs), TDeclaringType>(state =>
                {
                    object obj = default(TDeclaringType)!;
                    PopulateMemberInitializers(obj, memberInitializers, state.memberArgs);
                    return (TDeclaringType)obj!;
                });
            }

            static void PopulateMemberInitializers(object obj, MemberInitializerInfo[] memberInitializers, object?[] memberArgs)
            {
                for (int i = 0; i < memberInitializers.Length; i++)
                {
                    MemberInfo member = memberInitializers[i].Member;
                    if (member is PropertyInfo prop)
                    {
                        prop.SetValue(obj, memberArgs[i]);
                    }
                    else
                    {
                        ((FieldInfo)member).SetValue(obj, memberArgs[i]);
                    }
                }
            }
        }
    }

    private static Func<object?[]> CreateConstructorArgumentArrayFunc(ConstructorShapeInfo shapeInfo)
    {
        Debug.Assert(shapeInfo.MemberInitializers.Length == 0);
        int arity = shapeInfo.TotalParameters;
        if (arity == 0)
        {
            return static () => Array.Empty<object?>();
        }
        else if (shapeInfo.Parameters.Any(param => param.HasDefaultValue))
        {
            object?[] sourceParamArray = GetDefaultParameterArray(shapeInfo.Parameters);
            return () => (object?[])sourceParamArray.Clone();
        }
        else
        {
            return () => new object?[arity];
        }
    }

    private static Func<(object?[], object?[])> CreateConstructorAndMemberInitializerArgumentArrayFunc(ConstructorShapeInfo shapeInfo)
    {
        Debug.Assert(shapeInfo.MemberInitializers.Length > 0);
        int constructorParameterLength = shapeInfo.Parameters.Length;
        int memberInitializerLength = shapeInfo.MemberInitializers.Length;

        if (constructorParameterLength == 0)
        {
            return () => (Array.Empty<object?>(), new object?[memberInitializerLength]);
        }
        if (shapeInfo.Parameters.Any(param => param.HasDefaultValue))
        {
            object?[] sourceParamArray = GetDefaultParameterArray(shapeInfo.Parameters);
            return () => ((object?[])sourceParamArray.Clone(), new object?[memberInitializerLength]);
        }
        else
        {
            return () => (new object?[constructorParameterLength], new object?[memberInitializerLength]);
        }
    }

    private static object?[] GetDefaultParameterArray(ParameterInfo[] parameters)
        => parameters.Select(p => p.GetDefaultValueNormalized()).ToArray();
}
