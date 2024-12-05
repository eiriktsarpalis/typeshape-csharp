using PolyType.Abstractions;
using PolyType.ReflectionProvider.MemberAccessors;
using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace PolyType.ReflectionProvider;

/// <summary>
/// Provides a <see cref="ITypeShapeProvider"/> implementation that uses reflection.
/// </summary>
[RequiresUnreferencedCode(RequiresUnreferencedCodeMessage)]
[RequiresDynamicCode(RequiresDynamicCodeMessage)]
public class ReflectionTypeShapeProvider : ITypeShapeProvider
{
    internal const string RequiresUnreferencedCodeMessage = "PolyType Reflection provider requires unreferenced code.";
    internal const string RequiresDynamicCodeMessage = "PolyType Reflection provider requires dynamic code.";

    private static readonly ConcurrentDictionary<ReflectionTypeShapeProviderOptions, ReflectionTypeShapeProvider> s_providers = new();

    /// <summary>
    /// Gets the default provider instance using configuration supported by the current platform.
    /// </summary>
    public static ReflectionTypeShapeProvider Default { get; } = new ReflectionTypeShapeProvider(ReflectionTypeShapeProviderOptions.Default);

    /// <summary>
    /// Initializes a new instance of the <see cref="ReflectionTypeShapeProvider"/> class.
    /// </summary>
    /// <param name="options">The options governing the shape provider instance.</param>
    /// <returns>A <see cref="ReflectionTypeShapeProviderOptions"/> corresponding to the specified options.</returns>
    public static ReflectionTypeShapeProvider Create(ReflectionTypeShapeProviderOptions options)
    {
        Throw.IfNull(options);

        if (options == ReflectionTypeShapeProviderOptions.Default)
        {
            return Default;
        }

        return s_providers.GetOrAdd(options, _ => new ReflectionTypeShapeProvider(options));
    }

    private readonly ConcurrentDictionary<Type, ITypeShape> _cache = new();
    private readonly Func<Type, ITypeShape> _typeShapeFactory;

    private ReflectionTypeShapeProvider(ReflectionTypeShapeProviderOptions options)
    {
        if (options.UseReflectionEmit && !ReflectionHelpers.IsDynamicCodeSupported)
        {
            throw new PlatformNotSupportedException("Dynamic code generation is not supported on the current platform.");
        }

        Options = options;
        MemberAccessor = options.UseReflectionEmit
            ? new ReflectionEmitMemberAccessor()
            : new ReflectionMemberAccessor();

        _typeShapeFactory = CreateTypeShape;
    }

    /// <summary>
    /// Gets the configuration used by the provider.
    /// </summary>
    public ReflectionTypeShapeProviderOptions Options { get; }

    internal IReflectionMemberAccessor MemberAccessor { get; }

    /// <summary>
    /// Gets a <see cref="ITypeShape{T}"/> instance corresponding to the supplied type.
    /// </summary>
    /// <typeparam name="T">The type for which a shape is requested.</typeparam>
    /// <returns>
    /// A <see cref="ITypeShape{T}"/> instance corresponding to the current type.
    /// </returns>
    public ITypeShape<T> GetShape<T>() => (ITypeShape<T>)GetShape(typeof(T));

    /// <summary>
    /// Gets a <see cref="ITypeShape"/> instance corresponding to the supplied type.
    /// </summary>
    /// <param name="type">The type for which a shape is requested.</param>
    /// <returns>
    /// A <see cref="ITypeShape"/> instance corresponding to the current type.
    /// </returns>
    /// <exception cref="ArgumentNullException">The <paramref name="type"/> argument is null.</exception>
    /// <exception cref="ArgumentException">The <paramref name="type"/> cannot be a generic argument.</exception>
    public ITypeShape GetShape(Type type)
    {
        Throw.IfNull(type);
        return _cache.GetOrAdd(type, _typeShapeFactory);
    }

    private ITypeShape CreateTypeShape(Type type)
    {
        DebugExt.Assert(type != null);

        if (!type.CanBeGenericArgument())
        {
            throw new ArgumentException("Type cannot be a generic parameter", nameof(type));
        }

        return DetermineTypeKind(type) switch
        {
          TypeShapeKind.Enumerable => CreateEnumerableShape(type),
          TypeShapeKind.Dictionary => CreateDictionaryShape(type),
          TypeShapeKind.Enum => CreateEnumShape(type),
          TypeShapeKind.Nullable => CreateNullableShape(type),
          _ => CreateObjectShape(type),
        };
    }

    private ITypeShape CreateObjectShape(Type type)
    {
        Type objectShapeTy = typeof(ReflectionObjectTypeShape<>).MakeGenericType(type);
        return (ITypeShape)Activator.CreateInstance(objectShapeTy, this)!;
    }

    private IEnumerableTypeShape CreateEnumerableShape(Type type)
    {
        Debug.Assert(typeof(IEnumerable).IsAssignableFrom(type) || type.IsMemoryType(out _, out _));

        if (type.IsArray)
        {
            Type elementType = type.GetElementType()!;
            int rank = type.GetArrayRank();

            if (rank == 1)
            {
                Type enumerableTypeTy = typeof(ReflectionArrayTypeShape<>).MakeGenericType(elementType);
                return (IEnumerableTypeShape)Activator.CreateInstance(enumerableTypeTy, this)!;
            }
            else
            {
                Type enumerableTypeTy = typeof(MultiDimensionalArrayTypeShape<,>).MakeGenericType(type, elementType);
                return (IEnumerableTypeShape)Activator.CreateInstance(enumerableTypeTy, this, rank)!;
            }
        }

        foreach (Type interfaceTy in type.GetAllInterfaces())
        {
            if (interfaceTy.IsGenericType)
            {
                Type genericInterfaceTypeDef = interfaceTy.GetGenericTypeDefinition();

                if (genericInterfaceTypeDef == typeof(IEnumerable<>))
                {
                    Type elementType = interfaceTy.GetGenericArguments()[0];
                    Type enumerableTypeTy = typeof(ReflectionEnumerableTypeOfTShape<,>).MakeGenericType(type, elementType);
                    return (IEnumerableTypeShape)Activator.CreateInstance(enumerableTypeTy, this)!;
                }
            }
        }

        if (type.IsMemoryType(out Type? memoryElementType, out bool isReadOnlyMemory))
        {
            Type shapeType = isReadOnlyMemory ? typeof(ReadOnlyMemoryTypeShape<>) : typeof(MemoryTypeShape<>);
            Type enumerableTypeTy = shapeType.MakeGenericType(memoryElementType);
            return (IEnumerableTypeShape)Activator.CreateInstance(enumerableTypeTy, this)!;
        }

        Type enumerableType = typeof(ReflectionNonGenericEnumerableTypeShape<>).MakeGenericType(type);
        return (IEnumerableTypeShape)Activator.CreateInstance(enumerableType, this)!;
    }

    private IDictionaryTypeShape CreateDictionaryShape(Type type)
    {
        Type? dictionaryTypeTy = null;

        foreach (Type interfaceTy in type.GetAllInterfaces())
        {
            if (interfaceTy.IsGenericType)
            {
                Type genericInterfaceTy = interfaceTy.GetGenericTypeDefinition();
                Type[] genericArgs = interfaceTy.GetGenericArguments();

                if (genericInterfaceTy == typeof(IDictionary<,>))
                {
                    dictionaryTypeTy = typeof(ReflectionDictionaryOfTShape<,,>)
                        .MakeGenericType(type, genericArgs[0], genericArgs[1]);
                }
                else if (genericInterfaceTy == typeof(IReadOnlyDictionary<,>))
                {
                    dictionaryTypeTy = typeof(ReflectionReadOnlyDictionaryShape<,,>)
                        .MakeGenericType(type, genericArgs[0], genericArgs[1]);

                    break; // IReadOnlyDictionary takes precedence over IDictionary
                }
            }
        }

        if (dictionaryTypeTy is null)
        {
            Debug.Assert(typeof(IDictionary).IsAssignableFrom(type));
            dictionaryTypeTy = typeof(ReflectionNonGenericDictionaryShape<>).MakeGenericType(type);
        }

        return (IDictionaryTypeShape)Activator.CreateInstance(dictionaryTypeTy, this)!;
    }

    private IEnumTypeShape CreateEnumShape(Type enumType)
    {
        Debug.Assert(enumType.IsEnum);
        Type enumTypeTy = typeof(ReflectionEnumTypeShape<,>).MakeGenericType(enumType, Enum.GetUnderlyingType(enumType));
        return (IEnumTypeShape)Activator.CreateInstance(enumTypeTy, this)!;
    }

    private INullableTypeShape CreateNullableShape(Type nullableType)
    {
        Debug.Assert(nullableType.IsNullableStruct());
        Type nullableTypeTy = typeof(ReflectionNullableTypeShape<>).MakeGenericType(nullableType.GetGenericArguments());
        return (INullableTypeShape)Activator.CreateInstance(nullableTypeTy, this)!;
    }

    private static TypeShapeKind DetermineTypeKind(Type type)
    {
        if (type.IsEnum)
        {
            return TypeShapeKind.Enum;
        }

        if (Nullable.GetUnderlyingType(type) is not null)
        {
            return TypeShapeKind.Nullable;
        }

        if (typeof(IDictionary).IsAssignableFrom(type))
        {
            return TypeShapeKind.Dictionary;
        }
        else
        {
            foreach (Type interfaceTy in type.GetAllInterfaces())
            {
                if (interfaceTy.IsGenericType)
                {
                    Type genericInterfaceTy = interfaceTy.GetGenericTypeDefinition();
                    if (genericInterfaceTy == typeof(IDictionary<,>) ||
                        genericInterfaceTy == typeof(IReadOnlyDictionary<,>))
                    {
                        return TypeShapeKind.Dictionary;
                    }
                }
            }
        }

        if (typeof(IEnumerable).IsAssignableFrom(type) && type != typeof(string))
        {
            return TypeShapeKind.Enumerable;
        }

        if (type.IsMemoryType(out _, out _))
        {
            // Memory<T> or ReadOnlyMemory<T>
            return TypeShapeKind.Enumerable;
        }

        return TypeShapeKind.Object;
    }

    internal IPropertyShape CreateProperty(PropertyShapeInfo propertyShapeInfo)
    {
        Type memberType = propertyShapeInfo.MemberInfo.GetMemberType();
        Type reflectionPropertyType = typeof(ReflectionPropertyShape<,>).MakeGenericType(propertyShapeInfo.DeclaringType, memberType);
        return (IPropertyShape)Activator.CreateInstance(reflectionPropertyType, this, propertyShapeInfo)!;
    }

    internal IConstructorShape CreateConstructor(IConstructorShapeInfo ctorInfo)
    {
        Type argumentStateType = MemberAccessor.CreateConstructorArgumentStateType(ctorInfo);
        Type reflectionConstructorType = typeof(ReflectionConstructorShape<,>).MakeGenericType(ctorInfo.ConstructedType, argumentStateType);
        return (IConstructorShape)Activator.CreateInstance(reflectionConstructorType, this, ctorInfo)!;
    }

    internal IConstructorParameterShape CreateConstructorParameter(Type constructorArgumentState, IConstructorShapeInfo ctorInfo, int position)
    {
        IParameterShapeInfo parameterInfo = ctorInfo.Parameters[position];
        Type reflectionConstructorParameterType = typeof(ReflectionConstructorParameterShape<,>).MakeGenericType(constructorArgumentState, parameterInfo.Type);
        return (IConstructorParameterShape)Activator.CreateInstance(reflectionConstructorParameterType, this, ctorInfo, parameterInfo, position)!;
    }

    internal static IConstructorShapeInfo CreateTupleConstructorShapeInfo(Type tupleType)
    {
        Debug.Assert(tupleType.IsTupleType() && tupleType != typeof(ValueTuple));

        if (!tupleType.IsNestedTupleRepresentation())
        {
            // Treat non-nested tuples as regular types.
            ConstructorInfo ctorInfo = tupleType.GetConstructors()[0];
            MethodParameterShapeInfo[] parameters = ctorInfo.GetParameters().Select(p => new MethodParameterShapeInfo(p, isNonNullable: false)).ToArray();
            return new MethodConstructorShapeInfo(tupleType, ctorInfo, parameters);
        }

        return CreateNestedTupleCtorInfo(tupleType, offset: 0);

        static TupleConstructorShapeInfo CreateNestedTupleCtorInfo(Type tupleType, int offset)
        {
            Debug.Assert(tupleType.IsTupleType());
            ConstructorInfo ctorInfo = tupleType.GetConstructors()[0];
            ParameterInfo[] parameters = ctorInfo.GetParameters();
            MethodParameterShapeInfo[] ctorParameterInfo;
            TupleConstructorShapeInfo? nestedCtor;

            if (parameters.Length == 8 && parameters[7].ParameterType.IsTupleType())
            {
                ctorParameterInfo = MapParameterInfo(parameters.Take(7));
                nestedCtor = CreateNestedTupleCtorInfo(parameters[7].ParameterType, offset);
            }
            else
            {
                ctorParameterInfo = MapParameterInfo(parameters);
                nestedCtor = null;
            }

            return new TupleConstructorShapeInfo(tupleType, ctorInfo, ctorParameterInfo, nestedCtor);

            MethodParameterShapeInfo[] MapParameterInfo(IEnumerable<ParameterInfo> parameters)
                => parameters.Select(p => new MethodParameterShapeInfo(p, isNonNullable: false, logicalName: $"Item{++offset}")).ToArray();
        }
    }

    internal static NullabilityInfoContext? CreateNullabilityInfoContext()
    {
        return ReflectionHelpers.IsNullabilityInfoContextSupported ? new() : null;
    }
}
