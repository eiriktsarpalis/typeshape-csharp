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


    public Func<TDeclaringType> CreateDefaultConstructor<TDeclaringType>(IConstructorShapeInfo ctorInfo)
    {
        Debug.Assert(ctorInfo.Parameters.Count == 0);
        Debug.Assert(ctorInfo is MethodConstructorShapeInfo);
        return ((MethodConstructorShapeInfo)ctorInfo).ConstructorMethod is { } cI
            ? () => (TDeclaringType)cI.Invoke(null)
            : static () => default(TDeclaringType)!;
    }

    public Type CreateConstructorArgumentStateType(IConstructorShapeInfo ctorInfo)
    {
        return ctorInfo switch
        {
            { Parameters.Count: 1 } => ctorInfo.Parameters[0].Type,
            MethodConstructorShapeInfo { MemberInitializers.Length: > 0 } => typeof((object?[] ctorArgs, object[]? memberInitializerArgs)),
            _ => typeof(object?[])
        };
    }

    public Func<TArgumentState> CreateConstructorArgumentStateCtor<TArgumentState>(IConstructorShapeInfo ctorInfo)
    {
        if (ctorInfo.Parameters is [IParameterShapeInfo parameter])
        {
            Debug.Assert(typeof(TArgumentState) == parameter.Type);
            TArgumentState? defaultValue = parameter.HasDefaultValue
                ? (TArgumentState?)parameter.DefaultValue
                : default;

            return () => defaultValue!;
        }
        
        if (ctorInfo is MethodConstructorShapeInfo { MemberInitializers.Length: > 0 } ctor)
        {
            Debug.Assert(typeof(TArgumentState) == typeof((object?[], object?[])));
            return (Func<TArgumentState>)(object)CreateConstructorAndMemberInitializerArgumentArrayFunc(ctor);
        }

        Debug.Assert(typeof(TArgumentState) == typeof(object?[]));
        return (Func<TArgumentState>)(object)CreateConstructorArgumentArrayFunc(ctorInfo);
    }

    public Setter<TArgumentState, TParameter> CreateConstructorArgumentStateSetter<TArgumentState, TParameter>(IConstructorShapeInfo ctorInfo, int parameterIndex)
    {
        if (ctorInfo.Parameters.Count == 1)
        {
            Debug.Assert(parameterIndex == 0);
            Debug.Assert(typeof(TArgumentState) == typeof(TParameter));
            return (Setter<TArgumentState, TParameter>)(object)
                new Setter<TParameter, TParameter>(static (ref TParameter state, TParameter param) => state = param);
        }

        if (ctorInfo is MethodConstructorShapeInfo { MemberInitializers.Length: > 0 } ctor)
        {
            Debug.Assert(typeof(TArgumentState) == typeof((object?[], object?[])));
            if (parameterIndex < ctor.ConstructorParameters.Length)
            {
                return (Setter<TArgumentState, TParameter>)(object)
                    new Setter<(object?[], object?[]), TParameter>((ref (object?[] ctorArgs, object?[]) state, TParameter value) 
                        => state.ctorArgs[parameterIndex] = value);
            }
            else
            {
                int initializerIndex = parameterIndex - ctor.ConstructorParameters.Length;
                return (Setter<TArgumentState, TParameter>)(object)
                    new Setter<(object?[], object?[]), TParameter>((ref (object?[], object?[] memberArgs) state, TParameter value) 
                        => state.memberArgs[initializerIndex] = value);
            }
        }

        Debug.Assert(typeof(TArgumentState) == typeof(object?[]));
        return (Setter<TArgumentState, TParameter>)(object)
            new Setter<object?[], TParameter>((ref object?[] state, TParameter value) 
                => state[parameterIndex] = value);
    }

    public Func<TArgumentState, TDeclaringType> CreateParameterizedConstructor<TArgumentState, TDeclaringType>(IConstructorShapeInfo ctorInfo)
    {
        if (ctorInfo is TupleConstructorShapeInfo tupleCtor)
        {
            if (ctorInfo.Parameters is [IParameterShapeInfo param])
            {
                Debug.Assert(typeof(TArgumentState) == param.Type);
                Debug.Assert(tupleCtor.NestedTupleConstructor is null);
                ConstructorInfo ctor = tupleCtor.ConstructorInfo;
                return state => (TDeclaringType)ctor.Invoke(new object?[] { state });
            }

            Debug.Assert(typeof(TArgumentState) == typeof(object?[]));

            Stack<(ConstructorInfo, int)> ctorStack = new();
            for (TupleConstructorShapeInfo? current = tupleCtor; current != null; current = current.NestedTupleConstructor)
            {
                ctorStack.Push((current.ConstructorInfo, current.ConstructorParameters.Length));
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

        if (ctorInfo is MethodConstructorShapeInfo methodCtor)
        {
            MemberInitializerShapeInfo[] memberInitializers = methodCtor.MemberInitializers;
            if (memberInitializers.Length > 0)
            {
                if (memberInitializers is [MemberInitializerShapeInfo mI])
                {
                    Debug.Assert(typeof(TArgumentState) == mI.Type);
                    MemberInfo member = mI.MemberInfo;
                    if (methodCtor.ConstructorMethod is { } ctor)
                    {
                        Debug.Assert(ctor.GetParameters().Length == 0);

                        return state =>
                        {
                            object obj = ctor.Invoke();
                            PopulateMember(member, obj, state);
                            return (TDeclaringType)obj!;
                        };
                    }
                    else
                    {
                        return state =>
                        {
                            object obj = default(TDeclaringType)!;
                            PopulateMember(member, obj, state);
                            return (TDeclaringType)obj!;
                        };
                    }

                    static void PopulateMember(MemberInfo member, object obj, object? state)
                    {
                        if (member is PropertyInfo prop)
                        {
                            prop.SetValue(obj, state);
                        }
                        else
                        {
                            ((FieldInfo)member).SetValue(obj, state);
                        }
                    }
                }

                Debug.Assert(typeof(TArgumentState) == typeof((object?[], object?[])));

                if (methodCtor.ConstructorMethod is { } cI)
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
            else
            {
                if (methodCtor.Parameters is [MethodParameterShapeInfo pI])
                {
                    Debug.Assert(typeof(TArgumentState) == pI.Type);
                    Debug.Assert(methodCtor.ConstructorMethod != null);
                    MethodBase ctor = methodCtor.ConstructorMethod;
                    return state => (TDeclaringType)ctor.Invoke(new object?[] { state });
                }

                Debug.Assert(typeof(TArgumentState) == typeof(object?[]));
                return methodCtor.ConstructorMethod is { } cI
                    ? (Func<TArgumentState, TDeclaringType>)(object)new Func<object?[], TDeclaringType>(state => (TDeclaringType)cI.Invoke(state))
                    : static _ => default!;
            }
        }

        Debug.Fail($"Unrecognized constructor shape {ctorInfo}.");
        return null!;
    }

    private static Func<object?[]> CreateConstructorArgumentArrayFunc(IConstructorShapeInfo ctorInfo)
    {
        int arity = ctorInfo.Parameters.Count;
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

    private static Func<(object?[], object?[])> CreateConstructorAndMemberInitializerArgumentArrayFunc(MethodConstructorShapeInfo ctorInfo)
    {
        Debug.Assert(ctorInfo.MemberInitializers.Length > 0);
        int constructorParameterLength = ctorInfo.ConstructorParameters.Length;
        int memberInitializerLength = ctorInfo.MemberInitializers.Length;

        if (constructorParameterLength == 0)
        {
            return () => (Array.Empty<object?>(), new object?[memberInitializerLength]);
        }
        if (ctorInfo.ConstructorParameters.Any(param => param.HasDefaultValue))
        {
            object?[] sourceParamArray = GetDefaultParameterArray(ctorInfo.Parameters);
            return () => ((object?[])sourceParamArray.Clone(), new object?[memberInitializerLength]);
        }
        else
        {
            return () => (new object?[constructorParameterLength], new object?[memberInitializerLength]);
        }
    }

    private static object?[] GetDefaultParameterArray(IEnumerable<IParameterShapeInfo> parameters)
        => parameters.Select(p => p.DefaultValue).ToArray();
}
