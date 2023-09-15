using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using TypeShape.ReflectionProvider.MemberAccessors;

namespace TypeShape.ReflectionProvider;

/// <summary>
/// Provides a <see cref="ITypeShapeProvider"/> implementation that uses reflection.
/// </summary>
[RequiresUnreferencedCode("Reflection provider requires unreferenced code.")]
public class ReflectionTypeShapeProvider : ITypeShapeProvider
{
    /// <summary>
    /// Gets the default provider instance using configuration supported by the current platform.
    /// </summary>
    public static ReflectionTypeShapeProvider Default { get; } = new ReflectionTypeShapeProvider(useReflectionEmit: RuntimeFeature.IsDynamicCodeSupported);

    private readonly ConcurrentDictionary<Type, ITypeShape> _cache = new();

    /// <summary>
    /// Creates a new <see cref="ReflectionTypeShapeProvider"/> instance with provided configuration.
    /// </summary>
    /// <param name="useReflectionEmit">Specifies whether System.Reflection.Emit should be used when generating member accessors.</param>
    /// <exception cref="NotSupportedException">System.Reflection.Emit is not supported in this platform.</exception>
    public ReflectionTypeShapeProvider(bool useReflectionEmit)
    {
        if (useReflectionEmit && !RuntimeFeature.IsDynamicCodeSupported)
        {
            throw new NotSupportedException("System.Reflection.Emit is not supported in this platform.");
        }

        MemberAccessor = useReflectionEmit 
            ? new ReflectionEmitMemberAccessor() 
            : new ReflectionMemberAccessor();
    }

    /// <summary>
    /// Gets a <see cref="ITypeShape{T}"/> instance corresponding to the supplied type.
    /// </summary>
    /// <typeparam name="T">The type for which a shape is requested.</typeparam>
    /// <returns>
    /// A <see cref="ITypeShape{T}"/> instance corresponding to the current type.
    /// </returns>
    public ITypeShape<T> GetShape<T>() => 
        (ITypeShape<T>)_cache.GetOrAdd(typeof(T),
            static (_,@this) => new ReflectionTypeShape<T>(@this), this);

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
        ArgumentNullException.ThrowIfNull(type);
        return _cache.GetOrAdd(type, CreateType, this);
    }
    internal IReflectionMemberAccessor MemberAccessor { get; }

    private static ITypeShape CreateType(Type type, ReflectionTypeShapeProvider provider)
    {
        Debug.Assert(type != null);

        if (!type.CanBeGenericArgument())
        {
            throw new ArgumentException("Type cannot be a generic parameter", nameof(type));
        }

        Type reflectionType = typeof(ReflectionTypeShape<>).MakeGenericType(type);
        return (ITypeShape)Activator.CreateInstance(reflectionType, provider)!;
    }

    internal IPropertyShape CreateProperty(Type declaringType, MemberInfo memberInfo, MemberInfo[]? parentMembers, bool nonPublic, string? logicalName = null)
    {
        Debug.Assert(memberInfo is FieldInfo or PropertyInfo);

        Type memberType = memberInfo switch
        {
            FieldInfo f => f.FieldType,
            PropertyInfo p => p.PropertyType,
            _ => default!,
        };

        Type reflectionPropertyType = typeof(ReflectionPropertyShape<,>).MakeGenericType(declaringType, memberType);
        return (IPropertyShape)Activator.CreateInstance(reflectionPropertyType, this, logicalName, memberInfo, parentMembers, nonPublic)!;
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

    internal IEnumerableShape CreateEnumerableShape(Type type)
    {
        Debug.Assert(typeof(IEnumerable).IsAssignableFrom(type));

        if (type.IsArray)
        {
            Type enumerableTypeTy = typeof(ReflectionEnumerableShape<,>).MakeGenericType(type, type.GetElementType()!);
            return (IEnumerableShape)Activator.CreateInstance(enumerableTypeTy, this)!;
        }

        foreach (Type interfaceTy in type.GetAllInterfaces())
        {
            if (interfaceTy.IsGenericType)
            {
                Type genericInterfaceTypeDef = interfaceTy.GetGenericTypeDefinition();
                Type[] genericArgs = interfaceTy.GetGenericArguments();

                if (genericInterfaceTypeDef == typeof(ICollection<>))
                {
                    Type enumerableTypeTy = typeof(ReflectionCollectionShape<,>).MakeGenericType(type, genericArgs[0]);
                    return (IEnumerableShape)Activator.CreateInstance(enumerableTypeTy, this)!;
                }

                if (genericInterfaceTypeDef == typeof(IEnumerable<>))
                {
                    Type enumerableTypeTy = typeof(ReflectionEnumerableShape<,>).MakeGenericType(type, genericArgs[0]);
                    return (IEnumerableShape)Activator.CreateInstance(enumerableTypeTy, this)!;
                }
            }
        }

        Type enumerableType = typeof(IList).IsAssignableFrom(type)
            ? typeof(ReflectionListShape<>).MakeGenericType(type)
            : typeof(ReflectionEnumerableShape<>).MakeGenericType(type);

        return (IEnumerableShape)Activator.CreateInstance(enumerableType, this)!;
    }

    internal IDictionaryShape CreateDictionaryShape(Type type)
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
                    dictionaryTypeTy = typeof(ReflectionGenericDictionaryShape<,,>)
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
            dictionaryTypeTy = typeof(ReflectionDictionaryShape<>).MakeGenericType(type);
        }

        return (IDictionaryShape)Activator.CreateInstance(dictionaryTypeTy, this)!;
    }

    internal IEnumShape CreateEnumShape(Type enumType)
    {
        Debug.Assert(enumType.IsEnum);
        Type enumTypeTy = typeof(ReflectionEnumShape<,>).MakeGenericType(enumType, Enum.GetUnderlyingType(enumType));
        return (IEnumShape)Activator.CreateInstance(enumTypeTy, this)!;
    }

    internal INullableShape CreateNullableShape(Type nullableType)
    {
        Debug.Assert(nullableType.IsNullable());
        Type nullableTypeTy = typeof(ReflectionNullableShape<>).MakeGenericType(nullableType.GetGenericArguments());
        return (INullableShape)Activator.CreateInstance(nullableTypeTy, this)!;
    }
}
