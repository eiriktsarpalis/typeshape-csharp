using PolyType.Abstractions;
using PolyType.Examples.Utilities;
using PolyType.Utilities;

namespace PolyType.Examples.Cloner;

public static partial class Cloner
{
    private sealed class Builder(TypeGenerationContext generationContext) : TypeShapeVisitor, ITypeShapeFunc
    {
        private delegate void PropertyCloner<TSource, TTarget>(ref TSource source, ref TTarget target);
        private static readonly Dictionary<Type, object> s_builtInCloners = GetBuiltInCloners().ToDictionary();

        public Func<T?, T?> GetOrAddCloner<T>(ITypeShape<T> shape) => (Func<T?,T?>)generationContext.GetOrAdd(shape)!;

        object? ITypeShapeFunc.Invoke<T>(ITypeShape<T> shape, object? _)
        {
            if (s_builtInCloners.TryGetValue(typeof(T), out object? cloner))
            {
                return cloner;
            }

            return shape.Accept(this);
        }

        public override object? VisitObject<T>(IObjectTypeShape<T> typeShape, object? _)
        {
            if (typeof(T) == typeof(object))
            {
                return CreatePolymorphicCloner(generationContext.ParentCache!);
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
            var propertyTypeCloner = GetOrAddCloner(propertyShape.PropertyType);
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
                var elementCloner = parent.GetOrAddCloner(propertyShape.PropertyType);
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
            var elementCloner = GetOrAddCloner(enumerableShape.ElementType);
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

        public override object? VisitDictionary<TDictionary, TKey, TValue>(IDictionaryTypeShape<TDictionary, TKey, TValue> dictionaryShape, object? _)
        {
            var keyCloner = GetOrAddCloner(dictionaryShape.KeyType);
            var valueCloner = GetOrAddCloner(dictionaryShape.ValueType);
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
            var elementCloner = GetOrAddCloner(nullableShape.ElementType);
            return new Func<T?, T?>(t => t.HasValue ? elementCloner(t.Value) : null);
        }

        private static IEnumerable<KeyValuePair<Type, object>> GetBuiltInCloners()
        {
            yield return Create<string>(str => str);
            yield return Create<Version>(version => version is null ? null : new Version(version.Major, version.Minor, version.MinorRevision, version.Build));
            yield return Create<Uri>(uri => uri is null ? null : new Uri(uri.OriginalString));
            static KeyValuePair<Type, object> Create<T>(Func<T?, T?> cloner) => new(typeof(T), cloner);
        }

        private static Func<object?, object?> CreatePolymorphicCloner(TypeCache cache)
        {
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

                var derivedCloner = (Delegate)cache.GetOrAdd(runtimeType)!;
                return derivedCloner.DynamicInvoke(obj);
            };
        }

        private static NotSupportedException TypeNotCloneable<T>() => new($"The type '{typeof(T)}' is not cloneable.");
    }

    private sealed class DelayedClonerFactory : IDelayedValueFactory
    {
        public DelayedValue Create<T>(ITypeShape<T> _) =>
            new DelayedValue<Func<T?, T?>>(self => t => self.Result(t));
    }
}