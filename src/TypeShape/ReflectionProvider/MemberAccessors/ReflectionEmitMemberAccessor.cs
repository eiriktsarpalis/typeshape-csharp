using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;

namespace TypeShape.ReflectionProvider.MemberAccessors;

internal class ReflectionEmitMemberAccessor : IReflectionMemberAccessor
{
    // The maximum constructor arity for which to use ValueTuples when representing argument state.
    private const int MaxValueTupleArity = 21;

    public Getter<TDeclaringType, TPropertyType> CreateGetter<TDeclaringType, TPropertyType>(MemberInfo memberInfo)
    {
        Debug.Assert(memberInfo is FieldInfo or MemberInfo);

        DynamicMethod dynamicMethod = CreateDynamicMethod(memberInfo.Name, typeof(TPropertyType), new[] { typeof(TDeclaringType).MakeByRefType() });
        ILGenerator generator = dynamicMethod.GetILGenerator();

        generator.Emit(OpCodes.Ldarg_0);
        if (!typeof(TDeclaringType).IsValueType)
        {
            generator.Emit(OpCodes.Ldind_Ref);
        }

        switch (memberInfo)
        {
            case PropertyInfo prop:
                MethodInfo getter = prop.GetGetMethod(true)!;
                if (typeof(TDeclaringType).IsValueType)
                {
                    generator.EmitCall(OpCodes.Call, getter, null);
                }
                else
                {
                    generator.EmitCall(OpCodes.Callvirt, getter, null);
                }
                break;

            case FieldInfo field:
                generator.Emit(OpCodes.Ldfld, field);
                break;

            default:
                Debug.Fail("Unreachable code");
                break;
        }

        generator.Emit(OpCodes.Ret);
        return CreateDelegate<Getter<TDeclaringType, TPropertyType>>(dynamicMethod);
    }

    public Setter<TDeclaringType, TPropertyType> CreateSetter<TDeclaringType, TPropertyType>(MemberInfo memberInfo)
    {
        Debug.Assert(memberInfo is FieldInfo or MemberInfo);

        DynamicMethod dynamicMethod = CreateDynamicMethod(memberInfo.Name, typeof(void), new[] { typeof(TDeclaringType).MakeByRefType(), typeof(TPropertyType) });
        ILGenerator generator = dynamicMethod.GetILGenerator();

        generator.Emit(OpCodes.Ldarg_0);
        if (!typeof(TDeclaringType).IsValueType)
        {
            generator.Emit(OpCodes.Ldind_Ref);
        }

        generator.Emit(OpCodes.Ldarg_1);

        switch (memberInfo)
        {
            case PropertyInfo prop:
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
                generator.Emit(OpCodes.Stfld, field);
                break;

            default:
                Debug.Fail("Unreachable code");
                break;
        }

        generator.Emit(OpCodes.Ret);
        return CreateDelegate<Setter<TDeclaringType, TPropertyType>>(dynamicMethod);
    }

    public Setter<TEnumerable, TElement> CreateCollectionAddDelegate<TEnumerable, TElement>(MethodInfo methodInfo)
    {
        DynamicMethod dynamicMethod = CreateDynamicMethod(methodInfo.Name, typeof(void), new[] { typeof(TEnumerable).MakeByRefType(), typeof(TElement) });
        ILGenerator generator = dynamicMethod.GetILGenerator();

        generator.Emit(OpCodes.Ldarg_0);
        if (!typeof(TEnumerable).IsValueType)
        {
            generator.Emit(OpCodes.Ldind_Ref);
        }

        generator.Emit(OpCodes.Ldarg_1);

        if (typeof(TEnumerable).IsValueType)
        {
            generator.Emit(OpCodes.Call, methodInfo);
        }
        else
        {
            generator.Emit(OpCodes.Callvirt, methodInfo);
        }

        generator.Emit(OpCodes.Ret);
        return CreateDelegate<Setter<TEnumerable, TElement>>(dynamicMethod);
    }

    public Func<TDeclaringType> CreateDefaultConstructor<TDeclaringType>(ConstructorInfo? ctorInfo)
    {
        Debug.Assert(ctorInfo != null || typeof(TDeclaringType).IsValueType);

        if (ctorInfo is null)
        {
            return static () => default!;
        }

        DynamicMethod dynamicMethod = CreateDynamicMethod("defaultCtor", typeof(TDeclaringType), Array.Empty<Type>());
        ILGenerator generator = dynamicMethod.GetILGenerator();

        if (ctorInfo is null)
        {
            generator.Emit(OpCodes.Initobj, typeof(TDeclaringType));
        }
        else
        {
            generator.Emit(OpCodes.Newobj, ctorInfo);
        }

        generator.Emit(OpCodes.Ret);
        return CreateDelegate<Func<TDeclaringType>>(dynamicMethod);
    }

    public Type CreateConstructorArgumentStateType(ParameterInfo[] constructorParams)
    {
        return constructorParams.Length switch
        {
            0 => typeof(object),
            1 => constructorParams[0].ParameterType,
            <= MaxValueTupleArity => CreateValueTupleType(constructorParams.Select(p => p.ParameterType).ToArray()),
            _ => typeof(object?[]),
        };
    }

    public Func<TArgumentState> CreateConstructorArgumentStateCtor<TArgumentState>(ParameterInfo[] constructorParams)
    {
        int arity = constructorParams.Length;
        if (arity == 0)
        {
            Debug.Assert(typeof(TArgumentState) == typeof(object));
            return static () => default!;
        }
        else if (arity == 1)
        {
            Debug.Assert(typeof(TArgumentState) == constructorParams[0].ParameterType);
            if (constructorParams[0].HasDefaultValue)
            {
                TArgumentState argumentState = (TArgumentState)constructorParams[0].GetDefaultValueNormalized()!;
                return () => argumentState;
            }
            else
            {
                return () => default!;
            }
        }
        else if (arity <= MaxValueTupleArity)
        {
            Debug.Assert(typeof(TArgumentState).FullName!.StartsWith("System.ValueTuple`"));

            DynamicMethod dynamicMethod = CreateDynamicMethod("ctorArgumentStateCtor", typeof(TArgumentState), Array.Empty<Type>());
            ILGenerator generator = dynamicMethod.GetILGenerator();

            // Load the ValueTuple constructor parameters
            foreach (ParameterInfo param in constructorParams)
            {
                if (param.HasDefaultValue)
                {
                    object? defaultValue = param.GetDefaultValueNormalized(convertToParameterType: false);
                    LdLiteral(generator, param.ParameterType, defaultValue);
                }
                else
                {
                    LdDefaultValue(generator, param.ParameterType);
                }
            }

            // Emit the ValueTuple constructor opcodes
            EmitTupleCtor(typeof(TArgumentState), arity);
            void EmitTupleCtor(Type tupleType, int arity)
            {
                if (arity > 7)
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
        else
        {
            Debug.Assert(typeof(TArgumentState) == typeof(object?[]));
            return (Func<TArgumentState>)(object)ReflectionMemberAccessor.CreateConstructorArgumentArrayFunc(constructorParams);
        }
    }

    public Setter<TArgumentState, TParameter> CreateConstructorArgumentStateSetter<TArgumentState, TParameter>(int parameterIndex, int arity)
    {
        Debug.Assert(arity > 0);

        if (arity == 1)
        {
            Debug.Assert(parameterIndex == 0 && typeof(TArgumentState) == typeof(TParameter));
            return (Setter<TArgumentState, TParameter>)(object)new Setter<TParameter, TParameter>(static (ref TParameter state, TParameter value) => state = value);
        } 
        else if (arity <= MaxValueTupleArity)
        {
            Debug.Assert(typeof(TArgumentState).FullName!.StartsWith("System.ValueTuple`"));

            DynamicMethod dynamicMethod = CreateDynamicMethod("argumentStateSetter", typeof(void), new Type[] { typeof(TArgumentState).MakeByRefType(), typeof(TParameter) });
            ILGenerator generator = dynamicMethod.GetILGenerator();
            Type tupleType = typeof(TArgumentState);

            generator.Emit(OpCodes.Ldarg_0);

            while (parameterIndex > 6)
            {
                FieldInfo restField = tupleType.GetField("Rest", BindingFlags.Public | BindingFlags.Instance)!;
                generator.Emit(OpCodes.Ldflda, restField);
                tupleType = restField.FieldType;
                parameterIndex -= 7;
            }

            generator.Emit(OpCodes.Ldarg_1);
            FieldInfo element = tupleType.GetField($"Item{parameterIndex + 1}", BindingFlags.Public | BindingFlags.Instance)!;
            generator.Emit(OpCodes.Stfld, element);
            generator.Emit(OpCodes.Ret);

            return CreateDelegate<Setter<TArgumentState, TParameter>>(dynamicMethod);
        }
        else
        {
            Debug.Assert(typeof(TArgumentState) == typeof(object?[]));

            return (Setter<TArgumentState, TParameter>)(object)
                new Setter<object?[], TParameter>(
                    (ref object?[] state, TParameter setter) => state[parameterIndex] = setter);
        }
    }

    public Func<TArgumentState, TDeclaringType> CreateParameterizedConstructor<TArgumentState, TDeclaringType>(ConstructorInfo? ctorInfo, ParameterInfo[] parameters)
    {
        Debug.Assert(ctorInfo != null || (typeof(TDeclaringType).IsValueType && parameters.Length == 0));

        if (ctorInfo is null)
        {
            return static _ => default!;
        }

        DynamicMethod dynamicMethod = CreateDynamicMethod(ctorInfo.Name, typeof(TDeclaringType), new Type[] { typeof(TArgumentState) });
        ILGenerator generator = dynamicMethod.GetILGenerator();

        int arity = parameters.Length;
        if (arity == 0)
        {
            Debug.Assert(typeof(TArgumentState) == typeof(object));
            generator.Emit(OpCodes.Newobj, ctorInfo);
            generator.Emit(OpCodes.Ret);
        }
        else if (arity == 1)
        {
            Debug.Assert(typeof(TArgumentState) == parameters[0].ParameterType);
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Newobj, ctorInfo);
            generator.Emit(OpCodes.Ret);
        }
        else if (arity <= MaxValueTupleArity)
        {
            Debug.Assert(typeof(TArgumentState).FullName!.StartsWith("System.ValueTuple`"));
            Type tupleType = typeof(TArgumentState);
            List<FieldInfo> restFields = new();

            while (true)
            {
                FieldInfo[] elements = tupleType.GetFields(BindingFlags.Instance | BindingFlags.Public);

                bool hasNestedTuple = false;
                foreach (FieldInfo element in elements.OrderBy(e => e.Name))
                {
                    if (element.Name == "Rest")
                    {
                        restFields.Add(element);
                        tupleType = element.FieldType;
                        hasNestedTuple = true;
                        break;
                    }

                    generator.Emit(OpCodes.Ldarg_0);

                    foreach (FieldInfo restField in restFields)
                        generator.Emit(OpCodes.Ldfld, restField);

                    generator.Emit(OpCodes.Ldfld, element);
                }

                if (!hasNestedTuple)
                    break;
            }

            generator.Emit(OpCodes.Newobj, ctorInfo);
            generator.Emit(OpCodes.Ret);
        }
        else
        {
            Debug.Assert(typeof(TArgumentState) == typeof(object?[]));

            for (int i = 0; i < parameters.Length; i++)
            {
                Type parameterType = parameters[i].ParameterType;
                generator.Emit(OpCodes.Ldarg_0);
                generator.Emit(OpCodes.Ldc_I4, i);
                generator.Emit(OpCodes.Ldelem_Ref);

                if (parameterType.IsValueType)
                    generator.Emit(OpCodes.Unbox_Any, parameterType);
                else
                    generator.Emit(OpCodes.Castclass, parameterType);
            }

            generator.Emit(OpCodes.Newobj, ctorInfo);
            generator.Emit(OpCodes.Ret);
        }

        return CreateDelegate<Func<TArgumentState, TDeclaringType>>(dynamicMethod);
    }

    private static DynamicMethod CreateDynamicMethod(string name, Type returnType, Type[] parameters)
        => new DynamicMethod(name, returnType, parameters, typeof(ReflectionEmitMemberAccessor).Module, skipVisibility: true);

    private static TDelegate CreateDelegate<TDelegate>(DynamicMethod dynamicMethod)
        where TDelegate : Delegate
        => (TDelegate)dynamicMethod.CreateDelegate(typeof(TDelegate));

    private static void LdDefaultValue(ILGenerator generator, Type type)
    {
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

    private static void LdLiteral(ILGenerator generator, Type type, object? value)
    {
        if (type.IsEnum)
        {
            type = Enum.GetUnderlyingType(type);
            value = Convert.ChangeType(value, type);
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
                ConstructorInfo ctorInfo = type.GetConstructor(new Type[] { elementType })!;
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
                ConstructorInfo ctor = typeof(decimal).GetConstructor(new[] { typeof(int), typeof(int), typeof(int), typeof(bool), typeof(byte) })!;
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

    private static Type CreateValueTupleType(Type[] elementTypes)
    {
        Debug.Assert(elementTypes.Length > 0);

        return elementTypes.Length switch
        {
            1 => typeof(ValueTuple<>).MakeGenericType(elementTypes),
            2 => typeof(ValueTuple<,>).MakeGenericType(elementTypes),
            3 => typeof(ValueTuple<,,>).MakeGenericType(elementTypes),
            4 => typeof(ValueTuple<,,,>).MakeGenericType(elementTypes),
            5 => typeof(ValueTuple<,,,,>).MakeGenericType(elementTypes),
            6 => typeof(ValueTuple<,,,,,>).MakeGenericType(elementTypes),
            7 => typeof(ValueTuple<,,,,,,>).MakeGenericType(elementTypes),
            _ => typeof(ValueTuple<,,,,,,,>).MakeGenericType(elementTypes[..7].Append(CreateValueTupleType(elementTypes[7..])).ToArray()),
        };
    }
}
