using System.Collections.Concurrent;
using TypeShape.Abstractions;
using TypeShape.Examples.Utilities;

namespace TypeShape.Examples.Cloner;

/// <summary>
/// Provides an object graph deep cloning implementation built on top of TypeShape.
/// </summary>
public static class Cloner
{
    /// <summary>
    /// Builds a deep cloning delegate from the specified shape.
    /// </summary>
    /// <typeparam name="T">The type for which to build the cloner.</typeparam>
    /// <param name="shape">The shape instance guiding cloner construction.</param>
    /// <returns>A delegate for cloning instances of type <typeparamref name="T"/>.</returns>
    public static Func<T?, T?> CreateCloner<T>(ITypeShape<T> shape) =>
        new Builder().BuildCloner(shape);

    /// <summary>
    /// Builds a deep cloning delegate from the specified shape provider.
    /// </summary>
    /// <typeparam name="T">The type for which to build the cloner.</typeparam>
    /// <param name="shapeProvider">The shape provider guiding cloner construction.</param>
    /// <returns>A delegate for cloning instances of type <typeparamref name="T"/>.</returns>
    public static Func<T?, T?> CreateCloner<T>(ITypeShapeProvider shapeProvider) =>
        CreateCloner<T>(shapeProvider.Resolve<T>());

    /// <summary>
    /// Deep clones an instance of type <typeparamref name="T"/> using its <see cref="ITypeShape{T}"/> implementation.
    /// </summary>
    /// <typeparam name="T">The type of the value to be cloned.</typeparam>
    /// <param name="value">The value to be cloned.</param>
    /// <returns>A deep cloned copy of <paramref name="value"/>.</returns>
    public static T? Clone<T>(T? value) where T : IShapeable<T> =>
        ClonerCache<T, T>.Value(value);

    /// <summary>
    /// Deep clones an instance of type <typeparamref name="T"/> using an externally provider <see cref="ITypeShape{T}"/> implementation.
    /// </summary>
    /// <typeparam name="T">The type of the value to be cloned.</typeparam>
    /// <typeparam name="TProvider">The type providing an <see cref="IShapeable{T}"/> implementation.</typeparam>
    /// <param name="value">The value to be cloned.</param>
    /// <returns>A deep cloned copy of <paramref name="value"/>.</returns>
    public static T? Clone<T, TProvider>(T? value) where TProvider : IShapeable<T> =>
        ClonerCache<T, TProvider>.Value(value);

    private static class ClonerCache<T, TProvider> where TProvider : IShapeable<T>
    {
        public static Func<T?, T?> Value => s_value ??= CreateCloner<T>(TProvider.GetShape());
        private static Func<T?, T?>? s_value;
    }
    
    private sealed class Builder : TypeShapeVisitor, ITypeShapeFunc
    {
        private delegate void PropertyCloner<TSource, TTarget>(ref TSource source, ref TTarget target);
        private static readonly Dictionary<Type, object> s_builtInCloners = new(GetBuiltInCloners());
        private readonly TypeDictionary _cache = new();

        public Func<T?, T?> BuildCloner<T>(ITypeShape<T> shape) => 
            _cache.GetOrAdd<Func<T?, T?>>(shape, this, self => t => self.Result(t));
        
        public override object? VisitObject<T>(IObjectTypeShape<T> typeShape, object? _)
        {
            if (s_builtInCloners.TryGetValue(typeof(T), out object? cloner))
            {
                return cloner;
            }

            if (typeof(T) == typeof(object))
            {
                return CreatePolymorphicCloner(typeShape.Provider);
            }
            
            if (!typeShape.HasProperties && !typeShape.HasConstructor)
            {
                return new Func<T?, T?>(t => t);
            }

            IConstructorShape? ctor = typeShape.GetConstructor();
            return ctor != null ? ctor.Accept(this) : throw TypeNotCloneable<T>();
        }
        
        public override object? VisitConstructor<TDeclaringType, TArgumentState>(IConstructorShape<TDeclaringType, TArgumentState> constructorShape, object? _)
        {
            if (constructorShape.ParameterCount == 0)
            {
                var defaultCtor = constructorShape.GetDefaultConstructor();
                var propertyCloners = constructorShape.DeclaringType.GetProperties()
                    .Where(prop => prop.HasGetter && prop.HasSetter)
                    .Select(prop => (PropertyCloner<TDeclaringType, TDeclaringType>)prop.Accept(this)!)
                    .ToArray();

                return new Func<TDeclaringType, TDeclaringType>(source =>
                {
                    if (source is null)
                    {
                        return source;
                    }

                    TDeclaringType target = defaultCtor();
                    foreach (var propertyMapper in propertyCloners)
                    {
                        propertyMapper(ref source, ref target);
                    }

                    return target;
                });
            }
            
            var scopedBuilder = new ArgumentStateScopedBuilder<TArgumentState>(this);
            var argumentStateCtor = constructorShape.GetArgumentStateConstructor();
            var ctor = constructorShape.GetParameterizedConstructor();
            var propertyGetters = constructorShape.DeclaringType.GetProperties()
                .Where(prop => prop.HasGetter)
                .ToArray();

            PropertyCloner<TDeclaringType, TArgumentState>[] parameterMappers = constructorShape.GetParameters()
                .Select(param =>
                {
                    // Use case-insensitive comparison for constructor parameters, but case-sensitive for members.
                    StringComparison comparison = param.Kind is ConstructorParameterKind.ConstructorParameter
                        ? StringComparison.OrdinalIgnoreCase
                        : StringComparison.Ordinal;
                    
                    IPropertyShape? matchedProperty = propertyGetters.FirstOrDefault(getter => 
                        getter.PropertyType.Type == param.ParameterType.Type && 
                        string.Equals(getter.Name, param.Name, comparison));
                    
                    return (PropertyCloner<TDeclaringType, TArgumentState>?)matchedProperty?.Accept(scopedBuilder, state: param);
                })
                .Where(cloner => cloner != null)
                .ToArray()!;

            return new Func<TDeclaringType?, TDeclaringType?>(source =>
            {
                if (source is null)
                {
                    return source;
                }
                
                TArgumentState state = argumentStateCtor();
                foreach (PropertyCloner<TDeclaringType, TArgumentState> parameterMapper in parameterMappers)
                {
                    parameterMapper(ref source, ref state);
                }

                return ctor(ref state);
            });
        }

        public override object? VisitProperty<TDeclaringType, TPropertyType>(IPropertyShape<TDeclaringType, TPropertyType> propertyShape, object? _)
        {
            var propertyTypeCloner = BuildCloner(propertyShape.PropertyType);
            var getter = propertyShape.GetGetter();
            var setter = propertyShape.GetSetter();
            return new PropertyCloner<TDeclaringType, TDeclaringType>(
                (ref TDeclaringType source, ref TDeclaringType target) =>
                {
                    setter(ref target, propertyTypeCloner(getter(ref source))!);
                });
        }

        private sealed class ArgumentStateScopedBuilder<TArgumentState>(Builder parent) : TypeShapeVisitor
        {
            public override object? VisitProperty<TDeclaringType, TPropertyType>(IPropertyShape<TDeclaringType, TPropertyType> propertyShape, object? state)
            {
                var parameterShape = (IConstructorParameterShape<TArgumentState, TPropertyType>)state!;
                var elementCloner = parent.BuildCloner(propertyShape.PropertyType);
                var getter = propertyShape.GetGetter();
                var setter = parameterShape.GetSetter();
                return new PropertyCloner<TDeclaringType, TArgumentState>(
                    (ref TDeclaringType source, ref TArgumentState target) =>
                    {
                        TPropertyType value = getter(ref source);
                        setter(ref target, elementCloner(value)!);
                    });
            }
        }

        public override object? VisitEnumerable<TEnumerable, TElement>(IEnumerableTypeShape<TEnumerable, TElement> enumerableShape, object? _)
        {
            var elementCloner = BuildCloner(enumerableShape.ElementType);
            Func<TEnumerable, IEnumerable<TElement>> getEnumerable = enumerableShape.GetGetEnumerable();
            switch (enumerableShape.ConstructionStrategy)
            {
                case CollectionConstructionStrategy.Mutable:
                    var defaultCtor = enumerableShape.GetDefaultConstructor();
                    var addMember = enumerableShape.GetAddElement();
                    return new Func<TEnumerable?, TEnumerable?>(source =>
                    {
                        if (source is null)
                        {
                            return source;
                        }

                        var target = defaultCtor();
                        foreach (var element in getEnumerable(source))
                        {
                            addMember(ref target, elementCloner(element)!);
                        }

                        return target;
                    });
                
                case CollectionConstructionStrategy.Span:
                    var spanCtor = enumerableShape.GetSpanConstructor();
                    return new Func<TEnumerable?, TEnumerable?>(source =>
                    {
                        if (source is null)
                        {
                            return source;
                        }

                        TElement[] buffer = getEnumerable(source).ToArray();
                        for (int i = 0; i < buffer.Length; i++)
                        {
                            buffer[i] = elementCloner(buffer[i])!;
                        }
                        
                        return spanCtor(buffer);
                    });
                
                case CollectionConstructionStrategy.Enumerable:
                    var enumerableCtor = enumerableShape.GetEnumerableConstructor();
                    return new Func<TEnumerable?, TEnumerable?>(source =>
                    {
                        if (source is null)
                        {
                            return source;
                        }

                        return enumerableCtor(getEnumerable(source).Select(elementCloner)!);
                    });
                
                default:
                    throw TypeNotCloneable<TEnumerable>();
            }
        }

        public override object? VisitDictionary<TDictionary, TKey, TValue>(IDictionaryShape<TDictionary, TKey, TValue> dictionaryShape, object? _)
        {
            var keyCloner = BuildCloner(dictionaryShape.KeyType);
            var valueCloner = BuildCloner(dictionaryShape.ValueType);
            var getDictionary = dictionaryShape.GetGetDictionary();
            switch (dictionaryShape.ConstructionStrategy)
            {
                case CollectionConstructionStrategy.Mutable:
                    var defaultCtor = dictionaryShape.GetDefaultConstructor();
                    var addEntry = dictionaryShape.GetAddKeyValuePair();
                    return new Func<TDictionary?, TDictionary?>(source =>
                    {
                        if (source is null)
                        {
                            return source;
                        }

                        var target = defaultCtor();
                        foreach (var entry in getDictionary(source))
                        {
                            KeyValuePair<TKey, TValue> targetEntry = new(keyCloner(entry.Key)!, valueCloner(entry.Value)!);
                            addEntry(ref target, targetEntry);
                        }

                        return target;
                    });
                
                case CollectionConstructionStrategy.Span:
                    var spanCtor = dictionaryShape.GetSpanConstructor();
                    return new Func<TDictionary?, TDictionary?>(source =>
                    {
                        if (source is null)
                        {
                            return source;
                        }
                        
                        var buffer = getDictionary(source).ToArray();
                        for (int i = 0; i < buffer.Length; i++)
                        {
                            var sourceEntry = buffer[i];
                            buffer[i] = new(keyCloner(sourceEntry.Key)!, valueCloner(sourceEntry.Value)!);
                        }
                        
                        return spanCtor(buffer);
                    });
                
                case CollectionConstructionStrategy.Enumerable:
                    var enumerableCtor = dictionaryShape.GetEnumerableConstructor();
                    return new Func<TDictionary?, TDictionary?>(source =>
                    {
                        if (source is null)
                        {
                            return source;
                        }

                        return enumerableCtor(getDictionary(source).Select(e => new KeyValuePair<TKey, TValue>(keyCloner(e.Key)!, valueCloner(e.Value)!)));
                    });
                
                default:
                    throw TypeNotCloneable<TDictionary>();
            }
        }

        public override object? VisitEnum<TEnum, TUnderlying>(IEnumTypeShape<TEnum, TUnderlying> enumShape, object? _)
        {
            return new Func<TEnum, TEnum>(e => e);
        }

        public override object? VisitNullable<T>(INullableTypeShape<T> nullableShape, object? _)
        {
            var elementCloner = BuildCloner(nullableShape.ElementType);
            return new Func<T?, T?>(t => t.HasValue ? elementCloner(t.Value) : null);
        }
        
        private static IEnumerable<KeyValuePair<Type, object>> GetBuiltInCloners()
        {
            yield return Create<string>(str => str);
            yield return Create<Version>(version => version is null ? null : new Version(version.Major, version.Minor, version.MinorRevision, version.Build));
            yield return Create<Uri>(uri => uri is null ? null : new Uri(uri.OriginalString));
            static KeyValuePair<Type, object> Create<T>(Func<T?, T?> cloner) => new(typeof(T), cloner);
        }

        private static Func<object?, object?> CreatePolymorphicCloner(ITypeShapeProvider shapeProvider)
        {
            var cache = new ConcurrentDictionary<Type, Func<object?, object?>>();
            return obj =>
            {
                if (obj is null)
                {
                    return null;
                }

                Type runtimeType = obj.GetType();
                if (runtimeType == typeof(object))
                {
                    return new object();
                }

                var derivedCloner = cache.GetOrAdd(runtimeType, CreateCloner, shapeProvider);
                return derivedCloner(obj);

                static Func<object?, object?> CreateCloner(Type type, ITypeShapeProvider shapeProvider)
                {
                    ITypeShape shape = shapeProvider.Resolve(type);
                    ITypeShapeFunc func = new Builder();
                    return (Func<object?, object?>)shape.Invoke(func)!;
                }
            };
        }
        
        object? ITypeShapeFunc.Invoke<T>(ITypeShape<T> shape, object? _)
        {
            Func<T?, T?> cloner = BuildCloner(shape);
            return new Func<object?, object?>(source => cloner((T)source!));
        }

        private static NotSupportedException TypeNotCloneable<T>() => new($"The type '{typeof(T)}' is not cloneable.");
    }
}