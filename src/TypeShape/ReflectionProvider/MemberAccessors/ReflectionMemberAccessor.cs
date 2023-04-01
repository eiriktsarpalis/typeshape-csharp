using System.Diagnostics;
using System.Reflection;

namespace TypeShape.ReflectionProvider.MemberAccessors;

internal sealed class ReflectionMemberAccessor : IReflectionMemberAccessor
{
    public Getter<TDeclaringType, TPropertyType> CreateGetter<TDeclaringType, TPropertyType>(MemberInfo memberInfo, MemberInfo[]? parentMembers)
    {
        Debug.Assert(memberInfo is PropertyInfo or FieldInfo);

        if (parentMembers is null or { Length: 0 })
        {
            return memberInfo switch
            {
                PropertyInfo p => (ref TDeclaringType obj) => (TPropertyType)p.GetValue(obj)!,
                FieldInfo f => (ref TDeclaringType obj) => (TPropertyType)f.GetValue(obj)!,
                _ => default!,
            };
        }

        Debug.Assert(typeof(TDeclaringType).IsNestedTupleRepresentation());

        if (typeof(TDeclaringType).IsValueType)
        {
            Debug.Assert(memberInfo is FieldInfo);
            Debug.Assert(parentMembers is FieldInfo[]);

            var fieldInfo = (FieldInfo)memberInfo;
            var parentFields = (FieldInfo[])parentMembers;
            return (ref TDeclaringType obj) =>
            {
                object boxedObj = obj!;
                for (int i = 0; i < parentFields.Length; i++)
                {
                    boxedObj = parentFields[i].GetValue(boxedObj)!;
                }

                return (TPropertyType)fieldInfo.GetValue(boxedObj)!;
            };
        }
        else
        {
            Debug.Assert(memberInfo is PropertyInfo);
            Debug.Assert(parentMembers is PropertyInfo[]);

            var propertyInfo = (PropertyInfo)memberInfo;
            var parentProperties = (PropertyInfo[])parentMembers;
            return (ref TDeclaringType obj) =>
            {
                object boxedObj = obj!;
                for (int i = 0; i < parentProperties.Length; i++)
                {
                    boxedObj = parentProperties[i].GetValue(boxedObj)!;
                }

                return (TPropertyType)propertyInfo.GetValue(boxedObj)!;
            };
        }
    }

    public Setter<TDeclaringType, TPropertyType> CreateSetter<TDeclaringType, TPropertyType>(MemberInfo memberInfo, MemberInfo[]? parentMembers)
    {
        Debug.Assert(memberInfo is PropertyInfo or FieldInfo);

        if (parentMembers is null or { Length: 0 })
        {
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

        Debug.Assert(typeof(TDeclaringType).IsNestedTupleRepresentation());
        Debug.Assert(typeof(TDeclaringType).IsValueTupleType(), "only value tuples are mutable.");
        Debug.Assert(memberInfo is FieldInfo);
        Debug.Assert(parentMembers is FieldInfo[]);

        var fieldInfo = (FieldInfo)memberInfo;
        var parentFields = (FieldInfo[])parentMembers;
        return (ref TDeclaringType obj, TPropertyType value) =>
        {
            object?[] boxedValues = new object[parentFields.Length + 1];
            boxedValues[0] = obj;

            for (int i = 0; i < parentFields.Length; i++)
            {
                boxedValues[i + 1] = parentFields[i].GetValue(boxedValues[i]);
            }

            fieldInfo.SetValue(boxedValues[^1], value);

            for (int i = parentFields.Length - 1; i >= 0; i--)
            {
                parentFields[i].SetValue(boxedValues[i], boxedValues[i + 1]);
            }

            obj = (TDeclaringType)boxedValues[0]!;
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


    public Func<TDeclaringType> CreateDefaultConstructor<TDeclaringType>(ConstructorShapeInfo ctorInfo)
    {
        Debug.Assert(ctorInfo.TotalParameters == 0);
        Debug.Assert(ctorInfo.ConstructorInfo != null || typeof(TDeclaringType).IsValueType);
        return ctorInfo.ConstructorInfo is { } cI
            ? () => (TDeclaringType)cI.Invoke(null)!
            : static () => default(TDeclaringType)!;
    }

    public Type CreateConstructorArgumentStateType(ConstructorShapeInfo ctorInfo)
        => ctorInfo.MemberInitializers.Length == 0 ? typeof(object?[]) : typeof((object?[] ctorArgs, object?[] memberInitializerArgs));

    public Func<TArgumentState> CreateConstructorArgumentStateCtor<TArgumentState>(ConstructorShapeInfo ctorInfo)
    {
        if (ctorInfo.MemberInitializers.Length == 0)
        {
            Debug.Assert(typeof(TArgumentState) == typeof(object?[]));
            return (Func<TArgumentState>)(object)CreateConstructorArgumentArrayFunc(ctorInfo);
        }
        else
        {
            Debug.Assert(typeof(TArgumentState) == typeof((object?[], object?[])));
            return (Func<TArgumentState>)(object)CreateConstructorAndMemberInitializerArgumentArrayFunc(ctorInfo);
        }
    }

    public Setter<TArgumentState, TParameter> CreateConstructorArgumentStateSetter<TArgumentState, TParameter>(ConstructorShapeInfo ctorInfo, int parameterIndex)
    {
        if (ctorInfo.MemberInitializers.Length == 0)
        {
            Debug.Assert(typeof(TArgumentState) == typeof(object?[]));
            return (Setter<TArgumentState, TParameter>)(object)new Setter<object?[], TParameter>((ref object?[] state, TParameter value) => state[parameterIndex] = value);
        }

        Debug.Assert(typeof(TArgumentState) == typeof((object?[], object?[])));
        if (parameterIndex < ctorInfo.Parameters.Length)
        {
            return (Setter<TArgumentState, TParameter>)(object)new Setter<(object?[], object?[]), TParameter>((ref (object?[] ctorArgs, object?[]) state, TParameter value) => state.ctorArgs[parameterIndex] = value);
        }
        else
        {
            int initializerIndex = parameterIndex - ctorInfo.Parameters.Length;
            return (Setter<TArgumentState, TParameter>)(object)new Setter<(object?[], object?[]), TParameter>((ref (object?[], object?[] memberArgs) state, TParameter value) => state.memberArgs[initializerIndex] = value);
        }
    }

    public Func<TArgumentState, TDeclaringType> CreateParameterizedConstructor<TArgumentState, TDeclaringType>(ConstructorShapeInfo ctorInfo)
    {
        MemberInitializerShapeInfo[] memberInitializers = ctorInfo.MemberInitializers;

        if (ctorInfo.NestedTupleCtor != null)
        {
            Debug.Assert(memberInitializers.Length == 0);
            Debug.Assert(typeof(TArgumentState) == typeof(object?[]));

            Stack<(ConstructorInfo, int)> ctorStack = new();
            for (ConstructorShapeInfo? current = ctorInfo; current != null; current = current.NestedTupleCtor)
            {
                Debug.Assert(current.ConstructorInfo != null);
                ctorStack.Push((current.ConstructorInfo, current.Parameters.Length));
            }

            return (Func<TArgumentState, TDeclaringType>)(object)(new Func<object?[], TDeclaringType>(state =>
            {
                object? result = null;
                int i = state.Length;
                foreach ((ConstructorInfo ctorInfo, int arity) in ctorStack)
                {
                    object?[] localParams;
                    if (i == state.Length)
                    {
                        localParams = state[^arity..];
                    }
                    else
                    {
                        localParams = new object?[arity + 1];
                        state.AsSpan(i - arity, arity).CopyTo(localParams);
                        localParams[arity] = result;
                    }

                    result = ctorInfo.Invoke(localParams);
                    i -= arity;
                }

                return (TDeclaringType)result!;
            }));
        }
        if (memberInitializers.Length == 0)
        {
            Debug.Assert(typeof(TArgumentState) == typeof(object?[]));
            return ctorInfo.ConstructorInfo is { } cI
                ? (Func<TArgumentState, TDeclaringType>)(object)new Func<object?[], TDeclaringType>(state => (TDeclaringType)cI.Invoke(state)!)
                : static _ => default!;
        }
        else
        {
            Debug.Assert(typeof(TArgumentState) == typeof((object?[], object?[])));

            if (ctorInfo.ConstructorInfo is { } cI)
            {
                return (Func<TArgumentState, TDeclaringType>)(object)new Func<(object?[] ctorArgs, object?[] memberArgs), TDeclaringType>(state =>
                {
                    object obj = cI.Invoke(state.ctorArgs);
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

            static void PopulateMemberInitializers(object obj, MemberInitializerShapeInfo[] memberInitializers, object?[] memberArgs)
            {
                for (int i = 0; i < memberInitializers.Length; i++)
                {
                    MemberInfo member = memberInitializers[i].MemberInfo;

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

    private static Func<object?[]> CreateConstructorArgumentArrayFunc(ConstructorShapeInfo ctorInfo)
    {
        Debug.Assert(ctorInfo.MemberInitializers.Length == 0);
        int arity = ctorInfo.TotalParameters;
        if (arity == 0)
        {
            return static () => Array.Empty<object?>();
        }
        else if (ctorInfo.Parameters.Any(param => param.HasDefaultValue))
        {
            object?[] sourceParamArray = GetDefaultParameterArray(ctorInfo.Parameters);
            return () => (object?[])sourceParamArray.Clone();
        }
        else
        {
            return () => new object?[arity];
        }
    }

    private static Func<(object?[], object?[])> CreateConstructorAndMemberInitializerArgumentArrayFunc(ConstructorShapeInfo ctorInfo)
    {
        Debug.Assert(ctorInfo.MemberInitializers.Length > 0);
        int constructorParameterLength = ctorInfo.Parameters.Length;
        int memberInitializerLength = ctorInfo.MemberInitializers.Length;

        if (constructorParameterLength == 0)
        {
            return () => (Array.Empty<object?>(), new object?[memberInitializerLength]);
        }
        if (ctorInfo.Parameters.Any(param => param.HasDefaultValue))
        {
            object?[] sourceParamArray = GetDefaultParameterArray(ctorInfo.Parameters);
            return () => ((object?[])sourceParamArray.Clone(), new object?[memberInitializerLength]);
        }
        else
        {
            return () => (new object?[constructorParameterLength], new object?[memberInitializerLength]);
        }
    }

    private static object?[] GetDefaultParameterArray(ConstructorParameterShapeInfo[] parameters)
        => parameters.Select(p => p.DefaultValue).ToArray();
}
