using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using TypeShape.ReflectionProvider.MemberAccessors;

namespace TypeShape.ReflectionProvider;

[RequiresUnreferencedCode("Reflection provider requires unreferenced code.")]
public class ReflectionTypeShapeProvider : ITypeShapeProvider
{
    public static ReflectionTypeShapeProvider Default { get; } = new ReflectionTypeShapeProvider(useReflectionEmit: RuntimeFeature.IsDynamicCodeSupported);

    private readonly ConcurrentDictionary<Type, IType> _cache = new();

    public ReflectionTypeShapeProvider(bool useReflectionEmit)
    {
        if (useReflectionEmit && !RuntimeFeature.IsDynamicCodeSupported)
            throw new NotSupportedException("Reflection.Emit is not supported in this platform.");

        MemberAccessor = useReflectionEmit 
            ? new ReflectionEmitMemberAccessor() 
            : new ReflectionMemberAccessor();
    }

    internal IReflectionMemberAccessor MemberAccessor { get; }

    public IType<T> GetShape<T>() => (IType<T>)_cache.GetOrAdd(typeof(T), static (_,@this) => new ReflectionType<T>(@this), this);
    public IType GetShape(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return _cache.GetOrAdd(type, CreateType, this);
    }

    private static IType CreateType(Type type, ReflectionTypeShapeProvider provider)
    {
        Debug.Assert(type != null);

        if (!type.CanBeGenericArgument())
        {
            throw new ArgumentException("Type cannot be a generic parameter", nameof(type));
        }

        Type reflectionType = typeof(ReflectionType<>).MakeGenericType(type);
        return (IType)Activator.CreateInstance(reflectionType, provider)!;
    }

    internal IProperty CreateProperty(Type declaringType, MemberInfo memberInfo, MemberInfo[]? parentMembers, bool nonPublic, string? logicalName = null)
    {
        Debug.Assert(memberInfo is FieldInfo or PropertyInfo);

        Type memberType = memberInfo switch
        {
            FieldInfo f => f.FieldType,
            PropertyInfo p => p.PropertyType,
            _ => default!,
        };

        Type reflectionPropertyType = typeof(ReflectionProperty<,>).MakeGenericType(declaringType, memberType);
        return (IProperty)Activator.CreateInstance(reflectionPropertyType, this, logicalName, memberInfo, parentMembers, nonPublic)!;
    }

    internal IConstructor CreateConstructor(ConstructorShapeInfo shapeInfo)
    {
        Type argumentStateType = MemberAccessor.CreateConstructorArgumentStateType(shapeInfo);
        Type reflectionConstructorType = typeof(ReflectionConstructor<,>).MakeGenericType(shapeInfo.DeclaringType, argumentStateType);
        return (IConstructor)Activator.CreateInstance(reflectionConstructorType, this, shapeInfo)!;
    }

    internal IConstructorParameter CreateConstructorParameter(Type constructorArgumentState, ConstructorShapeInfo shapeInfo, int position)
    {
        Type reflectionConstructorParameterType = typeof(ReflectionConstructorParameter<,>).MakeGenericType(constructorArgumentState, shapeInfo[position].Type);
        return (IConstructorParameter)Activator.CreateInstance(reflectionConstructorParameterType, this, shapeInfo, position)!;
    }

    internal IEnumerableType CreateEnumerableType(Type type)
    {
        if (!typeof(IEnumerable).IsAssignableFrom(type))
        {
            throw new InvalidOperationException();
        }

        if (type.IsArray)
        {
            Type enumerableTypeTy = typeof(ReflectionEnumerableType<,>).MakeGenericType(type, type.GetElementType()!);
            return (IEnumerableType)Activator.CreateInstance(enumerableTypeTy, this)!;
        }

        foreach (Type interfaceTy in type.GetInterfaces())
        {
            if (interfaceTy.IsGenericType)
            {
                Type genericInterfaceTypeDef = interfaceTy.GetGenericTypeDefinition();
                Type[] genericArgs = interfaceTy.GetGenericArguments();

                if (genericInterfaceTypeDef == typeof(ICollection<>))
                {
                    Type enumerableTypeTy = typeof(ReflectionCollectionType<,>).MakeGenericType(type, genericArgs[0]);
                    return (IEnumerableType)Activator.CreateInstance(enumerableTypeTy, this)!;
                }

                if (genericInterfaceTypeDef == typeof(IEnumerable<>))
                {
                    Type enumerableTypeTy = typeof(ReflectionEnumerableType<,>).MakeGenericType(type, genericArgs[0]);
                    return (IEnumerableType)Activator.CreateInstance(enumerableTypeTy, this)!;
                }
            }
        }

        Type enumerableType = typeof(IList).IsAssignableFrom(type)
            ? typeof(ReflectionListType<>).MakeGenericType(type)
            : typeof(ReflectionEnumerableType<>).MakeGenericType(type);

        return (IEnumerableType)Activator.CreateInstance(enumerableType, this)!;
    }

    internal IDictionaryType CreateDictionaryType(Type type)
    {
        Type? dictionaryTypeTy = null;

        foreach (Type interfaceTy in type.GetInterfaces())
        {
            if (interfaceTy.IsGenericType)
            {
                Type genericInterfaceTy = interfaceTy.GetGenericTypeDefinition();
                Type[] genericArgs = interfaceTy.GetGenericArguments();

                if (genericInterfaceTy == typeof(IDictionary<,>))
                {
                    dictionaryTypeTy = typeof(ReflectionGenericDictionaryType<,,>)
                        .MakeGenericType(type, genericArgs[0], genericArgs[1]);
                }
                else if (genericInterfaceTy == typeof(IReadOnlyDictionary<,>))
                {
                    dictionaryTypeTy = typeof(ReflectionReadOnlyDictionaryType<,,>)
                        .MakeGenericType(type, genericArgs[0], genericArgs[1]);

                    break; // IReadOnlyDictionary takes precedence over IDictionary
                }
            }
        }

        if (dictionaryTypeTy is null)
        {
            if (!typeof(IDictionary).IsAssignableFrom(type))
            {
                throw new InvalidOperationException();
            }

            dictionaryTypeTy = typeof(ReflectionDictionaryType<>).MakeGenericType(type);
        }

        return (IDictionaryType)Activator.CreateInstance(dictionaryTypeTy, this)!;
    }

    internal IEnumType CreateEnumType(Type enumType)
    {
        if (!enumType.IsEnum)
            throw new InvalidOperationException();

        Type enumTypeTy = typeof(ReflectionEnumType<,>).MakeGenericType(enumType, Enum.GetUnderlyingType(enumType));
        return (IEnumType)Activator.CreateInstance(enumTypeTy, this)!;
    }

    internal INullableType CreateNullableType(Type nullableType)
    {
        if (!nullableType.IsGenericType || nullableType.GetGenericTypeDefinition() != typeof(Nullable<>))
            throw new InvalidOperationException();

        Type nullableTypeTy = typeof(ReflectionNullableType<>).MakeGenericType(nullableType.GetGenericArguments());
        return (INullableType)Activator.CreateInstance(nullableTypeTy, this)!;
    }
}
