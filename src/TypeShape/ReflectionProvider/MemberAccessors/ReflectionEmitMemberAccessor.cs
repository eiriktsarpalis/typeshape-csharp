using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;

namespace TypeShape.ReflectionProvider.MemberAccessors;

[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
internal sealed class ReflectionEmitMemberAccessor : IReflectionMemberAccessor
{
    public Getter<TDeclaringType, TPropertyType> CreateGetter<TDeclaringType, TPropertyType>(MemberInfo memberInfo, MemberInfo[]? parentMembers)
    {
        Debug.Assert(memberInfo is FieldInfo or MemberInfo);
        Debug.Assert(parentMembers is null || typeof(TDeclaringType).IsNestedTupleRepresentation());

        DynamicMethod dynamicMethod = CreateDynamicMethod(memberInfo.Name, typeof(TPropertyType), [typeof(TDeclaringType).MakeByRefType()]);
        ILGenerator generator = dynamicMethod.GetILGenerator();

        // return arg0.Member;
        generator.Emit(OpCodes.Ldarg_0);
        LdRef(generator, typeof(TDeclaringType));

        if (parentMembers != null)
        {
            foreach (MemberInfo parent in parentMembers)
            {
                EmitGet(parent, isParentMember: true);
            }
        }

        EmitGet(memberInfo, isParentMember: false);
        generator.Emit(OpCodes.Ret);

        return CreateDelegate<Getter<TDeclaringType, TPropertyType>>(dynamicMethod);

        void EmitGet(MemberInfo member, bool isParentMember)
        {
            switch (member)
            {
                case PropertyInfo prop:
                    Debug.Assert(prop.CanRead);
                    Debug.Assert(!isParentMember || !prop.DeclaringType!.IsValueType);

                    MethodInfo getter = prop.GetGetMethod(true)!;
                    if (getter.DeclaringType!.IsValueType)
                    {
                        generator.EmitCall(OpCodes.Call, getter, null);
                    }
                    else
                    {
                        generator.EmitCall(OpCodes.Callvirt, getter, null);
                    }
                    break;

                case FieldInfo field:
                    if (isParentMember)
                    {
                        generator.Emit(OpCodes.Ldflda, field);
                    }
                    else
                    {
                        generator.Emit(OpCodes.Ldfld, field);
                    }
                    break;

                default:
                    Debug.Fail("Unreachable code");
                    break;
            }
        }
    }

    public Setter<TDeclaringType, TPropertyType> CreateSetter<TDeclaringType, TPropertyType>(MemberInfo memberInfo, MemberInfo[]? parentMembers)
    {
        Debug.Assert(memberInfo is FieldInfo or MemberInfo);
        Debug.Assert(parentMembers is null || typeof(TDeclaringType).IsNestedTupleRepresentation());

        DynamicMethod dynamicMethod = CreateDynamicMethod(memberInfo.Name, typeof(void), [typeof(TDeclaringType).MakeByRefType(), typeof(TPropertyType)]);
        ILGenerator generator = dynamicMethod.GetILGenerator();

        // arg0.Member = arg1;
        generator.Emit(OpCodes.Ldarg_0);
        LdRef(generator, typeof(TDeclaringType));

        if (parentMembers != null)
        {
            Debug.Assert(parentMembers is FieldInfo[]);
            foreach (FieldInfo parentField in (FieldInfo[])parentMembers)
            {
                Debug.Assert(parentField.DeclaringType!.IsValueType);
                Debug.Assert(parentField.FieldType!.IsValueType);
                generator.Emit(OpCodes.Ldflda, parentField);
            }
        }

        generator.Emit(OpCodes.Ldarg_1);

        switch (memberInfo)
        {
            case PropertyInfo prop:
                Debug.Assert(prop.CanWrite);
                MethodInfo setter = prop.GetSetMethod(true)!;
                if (typeof(TDeclaringType).IsValueType)
                {
                    generator.EmitCall(OpCodes.Call, setter, null);
                }
                else
                {
                    generator.EmitCall(OpCodes.Callvirt, setter, null);
                }
                break;

            case FieldInfo field:
                Debug.Assert(!field.IsInitOnly);
                generator.Emit(OpCodes.Stfld, field);
                break;

            default:
                Debug.Fail("Unreachable code");
                break;
        }

        generator.Emit(OpCodes.Ret);
        return CreateDelegate<Setter<TDeclaringType, TPropertyType>>(dynamicMethod);
    }

    public Setter<TEnumerable, TElement> CreateEnumerableAddDelegate<TEnumerable, TElement>(MethodInfo methodInfo)
    {
        DynamicMethod dynamicMethod = CreateDynamicMethod(methodInfo.Name, typeof(void), [typeof(TEnumerable).MakeByRefType(), typeof(TElement)]);
        ILGenerator generator = dynamicMethod.GetILGenerator();

        // return arg0.Add(arg1);
        generator.Emit(OpCodes.Ldarg_0);
        LdRef(generator, typeof(TEnumerable));

        generator.Emit(OpCodes.Ldarg_1);

        if (typeof(TEnumerable).IsValueType)
        {
            generator.Emit(OpCodes.Call, methodInfo);
        }
        else
        {
            generator.Emit(OpCodes.Callvirt, methodInfo);
        }

        if (methodInfo.ReturnType != typeof(void))
        {
            generator.Emit(OpCodes.Pop);
        }

        generator.Emit(OpCodes.Ret);
        return CreateDelegate<Setter<TEnumerable, TElement>>(dynamicMethod);
    }

    public Setter<TDictionary, KeyValuePair<TKey, TValue>> CreateDictionaryAddDelegate<TDictionary, TKey, TValue>(MethodInfo methodInfo)
    {
        Type keyValuePairTy = typeof(KeyValuePair<TKey, TValue>);
        DynamicMethod dynamicMethod = CreateDynamicMethod(methodInfo.Name, typeof(void), [typeof(TDictionary).MakeByRefType(), keyValuePairTy]);
        ILGenerator generator = dynamicMethod.GetILGenerator();

        // return arg0.Add(arg1.Key, arg1.Value);
        generator.Emit(OpCodes.Ldarg_0);
        LdRef(generator, typeof(TDictionary));

        generator.Emit(OpCodes.Ldarga_S, 1);
        generator.Emit(OpCodes.Call, keyValuePairTy.GetMethod("get_Key", BindingFlags.Public | BindingFlags.Instance)!);

        generator.Emit(OpCodes.Ldarga_S, 1);
        generator.Emit(OpCodes.Call, keyValuePairTy.GetMethod("get_Value", BindingFlags.Public | BindingFlags.Instance)!);

        if (typeof(TDictionary).IsValueType)
        {
            generator.Emit(OpCodes.Call, methodInfo);
        }
        else
        {
            generator.Emit(OpCodes.Callvirt, methodInfo);
        }

        generator.Emit(OpCodes.Ret);
        return CreateDelegate<Setter<TDictionary, KeyValuePair<TKey, TValue>>>(dynamicMethod);
    }

    public Func<TDeclaringType> CreateDefaultConstructor<TDeclaringType>(IConstructorShapeInfo ctorInfo)
    {
        Debug.Assert(ctorInfo is MethodConstructorShapeInfo { Parameters: [] });

        var methodCtor = (MethodConstructorShapeInfo)ctorInfo;
        if (methodCtor.ConstructorMethod is null)
        {
            return static () => default!;
        }

        DynamicMethod dynamicMethod = CreateDynamicMethod("defaultCtor", typeof(TDeclaringType), []);
        ILGenerator generator = dynamicMethod.GetILGenerator();

        // return new TDeclaringType();
        EmitCall(generator, methodCtor.ConstructorMethod);
        generator.Emit(OpCodes.Ret);
        return CreateDelegate<Func<TDeclaringType>>(dynamicMethod);
    }

    public Type CreateConstructorArgumentStateType(IConstructorShapeInfo ctorInfo)
    {
        if (ctorInfo is TupleConstructorShapeInfo { IsValueTuple: true })
        {
            // Use the type itself as the argument state for value tuples.
            return ctorInfo.ConstructedType;
        }

        Type[] allParameterTypes = ctorInfo.Parameters
            .Select(p => p.Type)
            .ToArray();

        Type? flagType = GetMemberFlagType(ctorInfo, out _);

        return (allParameterTypes, flagType) switch
        {
            ([], _) => typeof(object), // use object for default ctors.
            ([Type t], null) => t, // use the type itself for single-parameter ctors.
            (_, null) => ReflectionHelpers.CreateValueTupleType(allParameterTypes), // use a value tuple for multiple parameters.
            (_, { }) => ReflectionHelpers.CreateValueTupleType([.. allParameterTypes, flagType]) // use a value tuple that includes the flag type.
        };
    }

    public Func<TArgumentState> CreateConstructorArgumentStateCtor<TArgumentState>(IConstructorShapeInfo ctorInfo)
    {
        if (ctorInfo is TupleConstructorShapeInfo { IsValueTuple: true })
        {
            Debug.Assert(typeof(TArgumentState) == ctorInfo.ConstructedType);
            return static () => default!;
        }
        else if (ctorInfo.Parameters is [])
        {
            Debug.Assert(typeof(TArgumentState) == typeof(object));
            return static () => default!;
        }
        else if (ctorInfo.Parameters is [MethodParameterShapeInfo parameter])
        {
            Debug.Assert(typeof(TArgumentState) == parameter.Type);
            if (parameter.HasDefaultValue)
            {
                TArgumentState argumentState = (TArgumentState)parameter.DefaultValue!;
                return () => argumentState;
            }
            else
            {
                return static () => default!;
            }
        }
        else
        {
            Debug.Assert(typeof(TArgumentState).IsValueTupleType());

            Type? memberFlagType = GetMemberFlagType(ctorInfo, out int totalFlags);
            if (memberFlagType != typeof(BitArray) && ctorInfo.Parameters.All(p => !p.HasDefaultValue))
            {
                // Just return the default tuple instance
                return static () => default!;
            }

            DynamicMethod dynamicMethod = CreateDynamicMethod("ctorArgumentStateCtor", typeof(TArgumentState), []);
            ILGenerator generator = dynamicMethod.GetILGenerator();

            // Load the default constructor arguments
            foreach (IParameterShapeInfo param in ctorInfo.Parameters)
            {
                if (param.HasDefaultValue)
                {
                    object? defaultValue = param.DefaultValue;
                    LdLiteral(generator, param.Type, defaultValue);
                }
                else
                {
                    LdDefaultValue(generator, param.Type);
                }
            }

            if (memberFlagType != null)
            {
                if (memberFlagType == typeof(BitArray))
                {
                    // Load a new BitArray with capacity that equals all member initializers
                    generator.Emit(OpCodes.Ldc_I4, totalFlags);
                    generator.Emit(OpCodes.Newobj, typeof(BitArray).GetConstructor([typeof(int)])!);
                }
                else
                {
                    Debug.Assert(memberFlagType == typeof(ulong));
                    LdDefaultValue(generator, memberFlagType);
                }
            }

            // Emit the ValueTuple constructor opcodes
            EmitTupleCtor(typeof(TArgumentState), ctorInfo.Parameters.Length);
            void EmitTupleCtor(Type tupleType, int arity)
            {
                if (arity > 7) // the tuple nests more tuple types
                {
                    // NB emit NewObj calls starting with innermost type first
                    EmitTupleCtor(tupleType.GetGenericArguments()[7], arity - 7);
                }

                ConstructorInfo ctorInfo = tupleType.GetConstructors()[0]!;
                generator.Emit(OpCodes.Newobj, ctorInfo);
            }

            generator.Emit(OpCodes.Ret);
            return CreateDelegate<Func<TArgumentState>>(dynamicMethod);
        }
    }

    public Setter<TArgumentState, TParameter> CreateConstructorArgumentStateSetter<TArgumentState, TParameter>(IConstructorShapeInfo ctorInfo, int parameterIndex)
    {
        Debug.Assert(ctorInfo.Parameters.Length > 0);

        if (ctorInfo.Parameters is [MethodParameterShapeInfo])
        {
            Debug.Assert(parameterIndex == 0 && typeof(TArgumentState) == typeof(TParameter));
            return (Setter<TArgumentState, TParameter>)(object)new Setter<TParameter, TParameter>(static (ref TParameter state, TParameter value) => state = value);
        } 
        else
        {
            Debug.Assert(typeof(TArgumentState).IsValueTupleType());

            DynamicMethod dynamicMethod = CreateDynamicMethod("argumentStateSetter", typeof(void), [typeof(TArgumentState).MakeByRefType(), typeof(TParameter)]);
            ILGenerator generator = dynamicMethod.GetILGenerator();
            Type tupleType = typeof(TArgumentState);

            // arg0.ItemN = arg1;
            generator.Emit(OpCodes.Ldarg_0);
            LdRef(generator, tupleType);
            FieldInfo element = LdNestedTuple(generator, tupleType, parameterIndex);
            generator.Emit(OpCodes.Ldarg_1);
            generator.Emit(OpCodes.Stfld, element);

            if (ctorInfo.Parameters[parameterIndex] is MemberInitializerShapeInfo)
            {
                Type? memberFlagType = GetMemberFlagType(ctorInfo, out int totalFlags);
                int initializerIndex = parameterIndex - ((MethodConstructorShapeInfo)ctorInfo).ConstructorParameters.Length;

                generator.Emit(OpCodes.Ldarg_0);
                LdRef(generator, tupleType);
                FieldInfo flagsElement = LdNestedTuple(generator, tupleType, ctorInfo.Parameters.Length);
                Debug.Assert(flagsElement.FieldType == memberFlagType);

                if (memberFlagType == typeof(ulong))
                {
                    Debug.Assert(initializerIndex <= 64);
                    // arg0.Flags |= 1L << initializerIndex;
                    generator.Emit(OpCodes.Ldflda, flagsElement);
                    generator.Emit(OpCodes.Dup);
                    generator.Emit(OpCodes.Ldind_I8);
                    generator.Emit(OpCodes.Ldc_I8, 1L << initializerIndex);
                    generator.Emit(OpCodes.Or);
                    generator.Emit(OpCodes.Stind_I8);
                }
                else
                {
                    Debug.Assert(memberFlagType == typeof(BitArray));
                    // arg0.Flags[initializerIndex] = true;
                    generator.Emit(OpCodes.Ldfld, flagsElement);
                    generator.Emit(OpCodes.Ldc_I4, initializerIndex);
                    generator.Emit(OpCodes.Ldc_I4_1);
                    generator.Emit(OpCodes.Callvirt, typeof(BitArray).GetMethod("set_Item", [typeof(int), typeof(bool)])!);
                }
            }

            generator.Emit(OpCodes.Ret);

            return CreateDelegate<Setter<TArgumentState, TParameter>>(dynamicMethod);
        }
    }

    public Constructor<TArgumentState, TDeclaringType> CreateParameterizedConstructor<TArgumentState, TDeclaringType>(IConstructorShapeInfo ctorInfo)
    {
        if (ctorInfo is MethodConstructorShapeInfo { ConstructorMethod: null, Parameters: [] })
        {
            Debug.Assert(typeof(TDeclaringType).IsValueType);
            return static (ref TArgumentState _) => default!;
        }
        else if (ctorInfo is TupleConstructorShapeInfo { IsValueTuple: true })
        {
            Debug.Assert(typeof(TDeclaringType) == typeof(TArgumentState));
            return (Constructor<TArgumentState, TDeclaringType>)(object)(new Constructor<TArgumentState, TArgumentState>(static (ref TArgumentState arg) => arg));
        }

        return CreateDelegate<Constructor<TArgumentState, TDeclaringType>>(EmitParameterizedConstructorMethod(typeof(TDeclaringType), typeof(TArgumentState), ctorInfo));
    }

    private static DynamicMethod EmitParameterizedConstructorMethod(Type declaringType, Type argumentStateType, IConstructorShapeInfo ctorInfo)
    {
        DynamicMethod dynamicMethod = CreateDynamicMethod("parameterizedCtor", declaringType, [argumentStateType.MakeByRefType()]);
        ILGenerator generator = dynamicMethod.GetILGenerator();

        if (ctorInfo is MethodConstructorShapeInfo methodCtor)
        {
            if (methodCtor.Parameters is [])
            {
                Debug.Assert(argumentStateType == typeof(object));
                Debug.Assert(methodCtor.ConstructorMethod != null);
                // return new TDeclaringType();
                EmitCall(generator, methodCtor.ConstructorMethod);
                generator.Emit(OpCodes.Ret);
            }
            else if (methodCtor.Parameters is [MethodParameterShapeInfo parameter])
            {
                Debug.Assert(argumentStateType == parameter.Type);

                Debug.Assert(methodCtor.ConstructorMethod != null);
                
                // return new TDeclaringType(state);
                generator.Emit(OpCodes.Ldarg_0);
                LdRef(generator, argumentStateType);
                EmitCall(generator, methodCtor.ConstructorMethod);
                generator.Emit(OpCodes.Ret);
            }
            else
            {
                Debug.Assert(argumentStateType.IsValueTupleType());

                if (methodCtor.MemberInitializers.Length == 0)
                {
                    // No member initializers -- just load all tuple elements and call the constructor
                    Debug.Assert(methodCtor.ConstructorMethod != null);

                    // return new TDeclaringType(state.Item1, state.Item2, ...);
                    foreach (var elementPath in ReflectionHelpers.EnumerateTupleMemberPaths(argumentStateType))
                    {
                        LdTupleElement(elementPath);
                    }

                    EmitCall(generator, methodCtor.ConstructorMethod);
                    generator.Emit(OpCodes.Ret);
                }
                else
                {
                    // Emit parameterized constructor + member initializers

                    (string LogicalName, MemberInfo Member, MemberInfo[]? ParentMembers)[] fieldPaths = ReflectionHelpers.EnumerateTupleMemberPaths(argumentStateType).ToArray();
                    Type? flagType = GetMemberFlagType(ctorInfo, out _);
                    Debug.Assert(flagType != null);

                    // var local = new TDeclaringType(state.Item1, state.Item2, ...);
                    LocalBuilder local = generator.DeclareLocal(declaringType);
                    if (methodCtor.ConstructorMethod is null)
                    {
                        Debug.Assert(declaringType.IsValueType);
                        generator.Emit(OpCodes.Ldloca_S, local);
                    }

                    int i = 0;
                    for (; i < methodCtor.ConstructorParameters.Length; i++)
                    {
                        LdTupleElement(fieldPaths[i]);
                    }

                    if (methodCtor.ConstructorMethod is null)
                    {
                        Debug.Assert(declaringType.IsValueType);
                        generator.Emit(OpCodes.Initobj, declaringType);
                    }
                    else
                    {
                        EmitCall(generator, methodCtor.ConstructorMethod);
                        generator.Emit(OpCodes.Stloc, local);
                    }

                    // Emit optional member initializers
                    foreach (MemberInitializerShapeInfo member in methodCtor.MemberInitializers)
                    {
                        // if (state.flags & (1L << memberIndex) != 0) local.member = state.ItemN;
                        generator.Emit(OpCodes.Ldarg_0);
                        Label label = EmitMemberFlagCheck(generator, flagType, i);

                        generator.Emit(declaringType.IsValueType ? OpCodes.Ldloca_S : OpCodes.Ldloc, local);
                        LdTupleElement(fieldPaths[i++]);
                        StMember(member);

                        generator.MarkLabel(label);
                    }

                    generator.Emit(declaringType.IsValueType ? OpCodes.Ldloc_S : OpCodes.Ldloc, local);
                    generator.Emit(OpCodes.Ret);

                    Label EmitMemberFlagCheck(ILGenerator generator, Type flagType, int index)
                    {
                        int memberIndex = index - methodCtor.ConstructorParameters.Length;
                        FieldInfo flagsField = LdNestedTuple(generator, argumentStateType, ctorInfo.Parameters.Length);
                        Label label = generator.DefineLabel();

                        if (flagType == typeof(ulong))
                        {
                            // if (state.flags & (1L << memberIndex) != 0)
                            generator.Emit(OpCodes.Ldfld, flagsField);
                            generator.Emit(OpCodes.Ldc_I8, 1L << memberIndex);
                            generator.Emit(OpCodes.And);
                            generator.Emit(OpCodes.Brfalse_S, label);
                        }
                        else
                        {
                            Debug.Assert(flagType == typeof(BitArray));

                            // if (state.flags[memberIndex])
                            generator.Emit(OpCodes.Ldfld, flagsField);
                            generator.Emit(OpCodes.Ldc_I4, memberIndex);
                            generator.Emit(OpCodes.Callvirt, typeof(BitArray).GetMethod("get_Item", [typeof(int)])!);
                            generator.Emit(OpCodes.Brfalse_S, label);
                        }

                        return label;
                    }
                }
            }
        }
        else if (ctorInfo is TupleConstructorShapeInfo tupleCtor)
        {
            Debug.Assert(argumentStateType.IsValueTupleType());
            Debug.Assert(!tupleCtor.IsValueTuple, "dynamic methods only needed for class tuple types");

            // return new Tuple<..,Tuple<..,Tuple<..>>>(state.Item1, state.Item2, ...);
            foreach (var elementPath in ReflectionHelpers.EnumerateTupleMemberPaths(argumentStateType))
            {
                LdTupleElement(elementPath);
            }

            var constructors = new Stack<ConstructorInfo>();
            for (TupleConstructorShapeInfo? curr = tupleCtor; curr != null; curr = curr.NestedTupleConstructor)
            {
                constructors.Push(curr.ConstructorInfo);
            }

            foreach (ConstructorInfo ctor in constructors)
            {
                generator.Emit(OpCodes.Newobj, ctor);
            }

            generator.Emit(OpCodes.Ret);
        }

        return dynamicMethod;

        void StMember(MemberInitializerShapeInfo memberInitializer)
        {
            switch (memberInitializer.MemberInfo)
            {
                case PropertyInfo prop:
                    Debug.Assert(prop.SetMethod != null);
                    OpCode callOp = declaringType.IsValueType ? OpCodes.Call : OpCodes.Callvirt;
                    generator.Emit(callOp, prop.SetMethod);
                    break;

                case FieldInfo field:
                    generator.Emit(OpCodes.Stfld, field);
                    break;

                default:
                    Debug.Fail("Invalid member");
                    break;
            }
        }

        void LdTupleElement((string LogicalName, MemberInfo Member, MemberInfo[]? ParentMembers) element)
        {
            Debug.Assert(element.Member is FieldInfo);
            Debug.Assert(element.ParentMembers is null or FieldInfo[]);

            generator.Emit(OpCodes.Ldarg_0);

            if (element.ParentMembers is FieldInfo[] parentMembers)
            {
                foreach (FieldInfo parent in parentMembers)
                {
                    generator.Emit(OpCodes.Ldflda, parent);
                }
            }

            generator.Emit(OpCodes.Ldfld, (FieldInfo)element.Member);
        }
    }

    private static DynamicMethod CreateDynamicMethod(string name, Type returnType, Type[] parameters)
        => new DynamicMethod(name, returnType, parameters, typeof(ReflectionEmitMemberAccessor).Module, skipVisibility: true);

    private static TDelegate CreateDelegate<TDelegate>(DynamicMethod dynamicMethod)
        where TDelegate : Delegate
        => (TDelegate)dynamicMethod.CreateDelegate(typeof(TDelegate));

    private static void LdDefaultValue(ILGenerator generator, Type type)
    {
        // Load the default value for the type onto the stack
        if (!type.IsValueType)
        {
            generator.Emit(OpCodes.Ldnull);
        }
        else if (
            type == typeof(bool) || type == typeof(byte) || type == typeof(sbyte) ||
            type == typeof(char) || type == typeof(ushort) || type == typeof(short) ||
            type == typeof(int) || type == typeof(uint))
        {
            generator.Emit(OpCodes.Ldc_I4_0);
        }
        else if (type == typeof(long) || type == typeof(ulong))
        {
            generator.Emit(OpCodes.Ldc_I4_0);
            generator.Emit(OpCodes.Conv_I8);
        }
        else if (type == typeof(float))
        {
            generator.Emit(OpCodes.Ldc_R4, 0f);
        }
        else if (type == typeof(double))
        {
            generator.Emit(OpCodes.Ldc_R8, 0d);
        }
        else if (type == typeof(IntPtr))
        {
            generator.Emit(OpCodes.Ldc_I4_0);
            generator.Emit(OpCodes.Conv_I);
        }
        else if (type == typeof(UIntPtr))
        {
            generator.Emit(OpCodes.Ldc_I4_0);
            generator.Emit(OpCodes.Conv_U);
        }
        else
        {
            LocalBuilder local = generator.DeclareLocal(type);
            generator.Emit(OpCodes.Ldloca_S, local.LocalIndex);
            generator.Emit(OpCodes.Initobj, type);
            generator.Emit(OpCodes.Ldloc, local.LocalIndex);
        }
    }

    public Func<T, TResult> CreateFuncDelegate<T, TResult>(ConstructorInfo ctorInfo)
        => CreateDelegate<Func<T, TResult>>(EmitConstructor(ctorInfo));

    public SpanConstructor<T, TResult> CreateSpanConstructorDelegate<T, TResult>(ConstructorInfo ctorInfo)
        => CreateDelegate<SpanConstructor<T, TResult>>(EmitConstructor(ctorInfo));

    private static DynamicMethod EmitConstructor(ConstructorInfo ctorInfo)
    {
        ParameterInfo[] parameters = ctorInfo.GetParameters();
        DynamicMethod dynamicMethod = CreateDynamicMethod("parameterizedCtor", ctorInfo.DeclaringType!, parameters.Select(p => p.ParameterType).ToArray());
        ILGenerator generator = dynamicMethod.GetILGenerator();

        for (int i = 0; i < parameters.Length; i++)
        {
            generator.Emit(OpCodes.Ldarg, i);
        }

        generator.Emit(OpCodes.Newobj, ctorInfo);
        generator.Emit(OpCodes.Ret);
        return dynamicMethod;
    }

    /// <summary>
    /// Returns the type used to track the set state of optional property setters.
    /// Use UInt64 for up to 64 optional properties, otherwise use BitArray.
    /// </summary>
    private static Type? GetMemberFlagType(IConstructorShapeInfo constructorShape, out int totalFlags)
    {
        if (constructorShape is MethodConstructorShapeInfo { MemberInitializers: [_, ..] memberInitializers })
        {
            totalFlags = memberInitializers.Length;
            return totalFlags <= 64 ? typeof(ulong) : typeof(BitArray);
        }

        totalFlags = 0;
        return null;
    }

    private static FieldInfo LdNestedTuple(ILGenerator generator, Type tupleType, int parameterIndex)
    {
        while (parameterIndex > 6) // The element we want to access is in a nested tuple
        {
            FieldInfo restField = tupleType.GetField("Rest", BindingFlags.Public | BindingFlags.Instance)!;
            generator.Emit(OpCodes.Ldflda, restField);
            tupleType = restField.FieldType;
            parameterIndex -= 7;
        }

        return tupleType.GetField($"Item{parameterIndex + 1}", BindingFlags.Public | BindingFlags.Instance)!;
    }

    private static void EmitCall(ILGenerator generator, MethodBase methodBase)
    {
        Debug.Assert(methodBase is MethodInfo or ConstructorInfo);
        if (methodBase is ConstructorInfo ctorInfo)
        {
            generator.Emit(OpCodes.Newobj, ctorInfo);
        }
        else
        {
            var methodInfo = (MethodInfo)methodBase!;
            OpCode opc = methodInfo.DeclaringType!.IsValueType || methodInfo.IsStatic 
                ? OpCodes.Call 
                : OpCodes.Callvirt;

            generator.Emit(opc, methodInfo);
        }
    }

    private static void LdLiteral(ILGenerator generator, Type type, object? value)
    {
        if (type.IsEnum)
        {
            type = Enum.GetUnderlyingType(type);
            value = Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
            LdLiteral(generator, type, value);
            return;
        }

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            if (value is null)
            {
                LocalBuilder local = generator.DeclareLocal(type);
                generator.Emit(OpCodes.Ldloca_S, local.LocalIndex);
                generator.Emit(OpCodes.Initobj, type);
                generator.Emit(OpCodes.Ldloc, local.LocalIndex);
            }
            else
            {
                Type elementType = type.GetGenericArguments()[0];
                Debug.Assert(value.GetType() != type);
                ConstructorInfo ctorInfo = type.GetConstructor([elementType])!;
                LdLiteral(generator, elementType, value);
                generator.Emit(OpCodes.Newobj, ctorInfo);
            }

            return;
        }

        switch (value)
        {
            case null:
                generator.Emit(OpCodes.Ldnull);
                break;
            case bool b:
                generator.Emit(OpCodes.Ldc_I4, b ? 1 : 0);
                break;
            case byte b:
                generator.Emit(OpCodes.Ldc_I4_S, b);
                break;
            case sbyte b:
                generator.Emit(OpCodes.Ldc_I4_S, b);
                break;
            case char c:
                generator.Emit(OpCodes.Ldc_I4, c);
                break;
            case ushort s:
                generator.Emit(OpCodes.Ldc_I4_S, s);
                break;
            case short s:
                generator.Emit(OpCodes.Ldc_I4_S, s);
                break;
            case int i:
                generator.Emit(OpCodes.Ldc_I4, i);
                break;
            case uint i:
                generator.Emit(OpCodes.Ldc_I4, i);
                break;
            case long i:
                generator.Emit(OpCodes.Ldc_I8, i);
                break;
            case ulong i:
                generator.Emit(OpCodes.Ldc_I8, (long)i);
                break;
            case float f:
                generator.Emit(OpCodes.Ldc_R4, f);
                break;
            case double d:
                generator.Emit(OpCodes.Ldc_R8, d);
                break;
            case string s:
                generator.Emit(OpCodes.Ldstr, s);
                break;
            case IntPtr ptr:
                generator.Emit(OpCodes.Ldc_I8, checked((long)ptr));
                generator.Emit(OpCodes.Conv_I);
                break;
            case UIntPtr ptr:
                generator.Emit(OpCodes.Ldc_I8, checked((ulong)ptr));
                generator.Emit(OpCodes.Conv_U);
                break;

            case decimal d:
                ConstructorInfo ctor = typeof(decimal).GetConstructor([typeof(int), typeof(int), typeof(int), typeof(bool), typeof(byte)])!;
                int[] bits = decimal.GetBits(d);
                bool sign = (bits[3] & 0x80000000) != 0;
                byte scale = (byte)(bits[3] >> 16 & 0x7F);

                generator.Emit(OpCodes.Ldc_I4, bits[0]);
                generator.Emit(OpCodes.Ldc_I4, bits[1]);
                generator.Emit(OpCodes.Ldc_I4, bits[2]);
                generator.Emit(OpCodes.Ldc_I4, sign ? 1 : 0);
                generator.Emit(OpCodes.Ldc_I4_S, scale);
                generator.Emit(OpCodes.Newobj, ctor);
                break;

            default:
                throw new NotImplementedException($"Default parameter support for {value.GetType()}");
        }
    }

    private static void LdRef(ILGenerator generator, Type type, bool copyValueTypes = false)
    {
        if (GetLdIndOpCode(type) is { } opcode)
        {
            generator.Emit(opcode);
        }
        else if (copyValueTypes || type.IsNullableStruct())
        {
            generator.Emit(OpCodes.Ldobj, type);
        }

        static OpCode? GetLdIndOpCode(Type argumentType)
        {
            if (!argumentType.IsValueType)
            {
                return OpCodes.Ldind_Ref;
            }

            if (argumentType.IsEnum)
            {
                argumentType = Enum.GetUnderlyingType(argumentType);
            }

            return Type.GetTypeCode(argumentType) switch
            {
                TypeCode.Boolean or TypeCode.Byte => OpCodes.Ldind_U1,
                TypeCode.SByte => OpCodes.Ldind_I1,
                TypeCode.Char or TypeCode.UInt16 => OpCodes.Ldind_U2,
                TypeCode.Int16 => OpCodes.Ldind_I2,
                TypeCode.UInt32 => OpCodes.Ldind_U4,
                TypeCode.Int32 => OpCodes.Ldind_I4,
                TypeCode.UInt64 or TypeCode.Int64 => OpCodes.Ldind_I8,
                TypeCode.Single => OpCodes.Ldind_R4,
                TypeCode.Double => OpCodes.Ldind_R8,
                _ => null,
            };
        }
    }
}
