using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace TypeShape.Applications.ObjectMapper;

public static partial class Mapper
{
    private delegate void PropertyMapper<TSource, TTarget>(ref TSource source, ref TTarget target);

    private sealed class Visitor : TypeShapeVisitor
    {
        private readonly TypeCache _cache = new();

        public Mapper<TSource, TTarget>? CreateMapper<TSource, TTarget>(ITypeShape<TSource> fromShape, ITypeShape<TTarget> toShape)
        {
            if (TryGetCachedResult(out Mapper<TSource, TTarget>? mapper))
            {
                return mapper;
            }

            mapper = CreateMapperCore(fromShape, toShape);
            return CacheResult(mapper);
        }

        private Mapper<TSource, TTarget>? CreateMapperCore<TSource, TTarget>(ITypeShape<TSource> sourceShape, ITypeShape<TTarget> targetShape)
        {
            if (sourceShape.Kind != targetShape.Kind)
            {
                // For simplicity, only map between types of matching kind.
                return null;
            }

            if (sourceShape.Kind is TypeKind.None or TypeKind.Enum ||
                typeof(TSource) == typeof(BigInteger) ||
                typeof(TSource) == typeof(object))
            {
                // Only map if TSource is a subtype of TTarget.
                if (typeof(TTarget).IsAssignableFrom(typeof(TSource)))
                {
                    return (Mapper<TSource, TTarget>)(object)(new Mapper<TSource, TSource>(source => source));
                }

                // For simplicity, do not support conversion between numeric/string types.
                return null;
            }

            switch (sourceShape.Kind)
            {
                case TypeKind.Nullable:
                    INullableShape sourceNullable = sourceShape.GetNullableShape();
                    INullableShape targetNullable = targetShape.GetNullableShape();
                    return (Mapper<TSource, TTarget>?)sourceNullable.Accept(this, targetNullable);

                case TypeKind.Enumerable:
                    IEnumerableShape sourceEnumerable = sourceShape.GetEnumerableShape();
                    IEnumerableShape targetEnumerable = targetShape.GetEnumerableShape();
                    return (Mapper<TSource, TTarget>?)sourceEnumerable.Accept(this, targetEnumerable);

                case TypeKind.Dictionary:
                    IDictionaryShape sourceDictionary = sourceShape.GetDictionaryShape();
                    IDictionaryShape targetDictionary = targetShape.GetDictionaryShape();
                    return (Mapper<TSource, TTarget>?)sourceDictionary.Accept(this, targetDictionary);

                default:
                    Debug.Assert(sourceShape.Kind == TypeKind.Object);
                    IConstructorShape? ctor = targetShape
                        .GetConstructors(includeProperties: true, includeFields: true)
                        .MinBy(ctor => ctor.ParameterCount);

                    if (ctor is null)
                    {
                        return null; // TTarget is not constructible, so we can't map to it.
                    }

                    IPropertyShape[] sourceGetters = sourceShape.GetProperties(includeFields: true)
                        .Where(prop => prop.HasGetter)
                        .ToArray();

                    var visitor = new TypeScopedVisitor<TSource>(this);
                    return (Mapper<TSource, TTarget>?)ctor.Accept(visitor, sourceGetters);
            }
        }

        public override object? VisitProperty<TSource, TSourceProperty>(IPropertyShape<TSource, TSourceProperty> sourceGetter, object? state)
        {
            Debug.Assert(state is IPropertyShape or IConstructorParameterShape);
            var visitor = new PropertyScopedVisitor<TSource, TSourceProperty>(this);
            return state is IPropertyShape targetProp
                ? targetProp.Accept(visitor, sourceGetter)
                : ((IConstructorParameterShape)state).Accept(visitor, sourceGetter);
        }

        public override object? VisitNullable<TSource>(INullableShape<TSource> sourceNullable, object? state)
        {
            var targetNullable = (INullableShape)state!;
            var visitor = new NullableScopedVisitor<TSource>(this);
            return targetNullable.Accept(visitor, sourceNullable);
        }

        public override object? VisitEnumerable<TSource, TSourceElement>(IEnumerableShape<TSource, TSourceElement> sourceEnumerable, object? state)
        {
            var targetEnumerable = (IEnumerableShape)state!;
            var visitor = new EnumerableScopedVisitor<TSource, TSourceElement>(this);
            return targetEnumerable.Accept(visitor, sourceEnumerable);
        }

        public override object? VisitDictionary<TSourceDictionary, TSourceKey, TSourceValue>(IDictionaryShape<TSourceDictionary, TSourceKey, TSourceValue> sourceDictionary, object? state)
        {
            var targetDictionary = (IDictionaryShape)state!;
            var visitor = new DictionaryScopedVisitor<TSourceDictionary, TSourceKey, TSourceValue>(this);
            return targetDictionary.Accept(visitor, sourceDictionary);
        }

        private sealed class TypeScopedVisitor<TSource>(Visitor baseVisitor) : TypeShapeVisitor
        {
            public override object? VisitConstructor<TTarget, TArgumentState>(IConstructorShape<TTarget, TArgumentState> targetCtor, object? state)
            {
                var sourceGetters = (IPropertyShape[])state!;
                if (targetCtor.ParameterCount == 0)
                {
                    Func<TTarget> defaultCtor = targetCtor.GetDefaultConstructor();
                    PropertyMapper<TSource, TTarget>[] propertyMappers = targetCtor.DeclaringType.GetProperties(includeFields: true)
                        .Where(prop => prop.HasSetter)
                        .Select(setter =>
                            sourceGetters.FirstOrDefault(getter => getter.Name == setter.Name) is { } getter
                            ? (PropertyMapper<TSource, TTarget>?)getter.Accept(baseVisitor, setter)
                            : null)
                        .Where(mapper => mapper != null)
                        .ToArray()!;

                    return new Mapper<TSource, TTarget>(source =>
                    {
                        if (source is null)
                        {
                            return default;
                        }

                        TTarget? target = defaultCtor();
                        foreach (PropertyMapper<TSource, TTarget> mapper in propertyMappers)
                        {
                            mapper(ref source, ref target);
                        }

                        return target;
                    });
                }
                else
                {
                    Func<TArgumentState> argumentStateCtor = targetCtor.GetArgumentStateConstructor();
                    Constructor<TArgumentState, TTarget> ctor = targetCtor.GetParameterizedConstructor();
                    PropertyMapper<TSource, TArgumentState>[] propertyMappers = targetCtor.GetParameters()
                        .Select(targetParam =>
                        {
                            // Use case-insensitive comparison for constructor parameters, but case-sensitive for members.
                            StringComparison comparison = targetParam.Kind is ConstructorParameterKind.ConstructorParameter
                                ? StringComparison.OrdinalIgnoreCase
                                : StringComparison.Ordinal;

                            var mapper = sourceGetters.FirstOrDefault(getter => string.Equals(getter.Name, targetParam.Name, comparison)) is { } getter
                                ? (PropertyMapper<TSource, TArgumentState>?)getter.Accept(baseVisitor, targetParam)
                                : null;

                            if (mapper is null && targetParam.IsRequired)
                            {
                                ThrowCannotMapTypes(typeof(TSource), typeof(TTarget));
                            }

                            return mapper;
                        })
                        .Where(mapper => mapper != null)
                        .ToArray()!;

                    return new Mapper<TSource, TTarget>(source =>
                    {
                        if (source is null)
                        {
                            return default;
                        }

                        TArgumentState? state = argumentStateCtor();
                        foreach (PropertyMapper<TSource, TArgumentState> mapper in propertyMappers)
                        {
                            mapper(ref source, ref state);
                        }

                        return ctor(ref state);
                    });
                }
            }
        }

        private sealed class PropertyScopedVisitor<TSource, TSourceProperty>(Visitor baseVisitor) : TypeShapeVisitor
        {
            public override object? VisitProperty<TTarget, TTargetProperty>(IPropertyShape<TTarget, TTargetProperty> targetProperty, object? state)
            {
                IPropertyShape<TSource, TSourceProperty> sourceProperty = (IPropertyShape<TSource, TSourceProperty>)state!;
                var propertyTypeMapper = baseVisitor.CreateMapper(sourceProperty.PropertyType, targetProperty.PropertyType);
                if (propertyTypeMapper is null)
                    return null;

                Getter<TSource, TSourceProperty> sourceGetter = sourceProperty.GetGetter();
                Setter<TTarget, TTargetProperty> targetSetter = targetProperty.GetSetter();
                return new PropertyMapper<TSource, TTarget>((ref TSource source, ref TTarget target) =>
                {
                    TSourceProperty sourcePropertyValue = sourceGetter(ref source);
                    TTargetProperty targetPropertyValue = propertyTypeMapper(sourcePropertyValue)!;
                    targetSetter(ref target, targetPropertyValue);
                });
            }

            public override object? VisitConstructorParameter<TArgumentState, TTargetParameter>(IConstructorParameterShape<TArgumentState, TTargetParameter> targetParameter, object? state)
            {
                var sourceProperty = (IPropertyShape<TSource, TSourceProperty>)state!;
                var propertyTypeMapper = baseVisitor.CreateMapper(sourceProperty.PropertyType, targetParameter.ParameterType);
                if (propertyTypeMapper is null)
                    return null;

                Getter<TSource, TSourceProperty> sourceGetter = sourceProperty.GetGetter();
                Setter<TArgumentState, TTargetParameter> parameterSetter = targetParameter.GetSetter();
                return new PropertyMapper<TSource, TArgumentState>((ref TSource source, ref TArgumentState target) =>
                {
                    TSourceProperty sourcePropertyValue = sourceGetter(ref source);
                    TTargetParameter targetParameterValue = propertyTypeMapper(sourcePropertyValue)!;
                    parameterSetter(ref target, targetParameterValue);
                });
            }
        }

        private sealed class NullableScopedVisitor<TSourceElement>(Visitor baseVisitor) : TypeShapeVisitor
            where TSourceElement : struct
        {
            public override object? VisitNullable<TTargetElement>(INullableShape<TTargetElement> targetNullable, object? state)
                where TTargetElement : struct
            {
                var sourceNullable = (INullableShape<TSourceElement>)state!;
                var elementMapper = baseVisitor.CreateMapper(sourceNullable.ElementType, targetNullable.ElementType);
                if (elementMapper is null)
                    return null;

                return new Mapper<TSourceElement?, TTargetElement?>(source => source is null ? null : elementMapper(source.Value));
            }
        }

        private sealed class EnumerableScopedVisitor<TSourceEnumerable, TSourceElement>(Visitor baseVisitor) : TypeShapeVisitor
        {
            public override object? VisitEnumerable<TTargetEnumerable, TTargetElement>(IEnumerableShape<TTargetEnumerable, TTargetElement> targetEnumerable, object? state)
            {
                var sourceEnumerable = (IEnumerableShape<TSourceEnumerable, TSourceElement>)state!;
                var sourceGetEnumerable = sourceEnumerable.GetGetEnumerable();

                var elementMapper = baseVisitor.CreateMapper(sourceEnumerable.ElementType, targetEnumerable.ElementType);
                if (elementMapper is null)
                    return null;

                switch (targetEnumerable.ConstructionStrategy)
                {
                    case CollectionConstructionStrategy.Mutable:
                        var defaultCtor = targetEnumerable.GetDefaultConstructor();
                        var addElement = targetEnumerable.GetAddElement();
                        return new Mapper<TSourceEnumerable, TTargetEnumerable>(source =>
                        {
                            if (source is null)
                            {
                                return default;
                            }

                            var target = defaultCtor();
                            foreach (TSourceElement sourceElement in sourceGetEnumerable(source))
                            {
                                addElement(ref target, elementMapper(sourceElement)!);
                            }

                            return target;
                        });

                    case CollectionConstructionStrategy.Enumerable:
                        var createEnumerable = targetEnumerable.GetEnumerableConstructor();
                        return new Mapper<TSourceEnumerable, TTargetEnumerable>(source =>
                        {
                            if (source is null)
                            {
                                return default;
                            }

                            return createEnumerable(sourceGetEnumerable(source).Select(e => elementMapper(e!)));
                        });

                    case CollectionConstructionStrategy.Span:
                        var createSpan = targetEnumerable.GetSpanConstructor();
                        return new Mapper<TSourceEnumerable, TTargetEnumerable>(source =>
                        {
                            if (source is null)
                            {
                                return default;
                            }

                            return createSpan(sourceGetEnumerable(source).Select(e => elementMapper(e!)).ToArray());
                        });

                    default:
                        return null;
                }       
            }
        }

        private sealed class DictionaryScopedVisitor<TSourceDictionary, TSourceKey, TSourceValue>(Visitor baseVisitor) : TypeShapeVisitor
            where TSourceKey : notnull
        {
            public override object? VisitDictionary<TTargetDictionary, TTargetKey, TTargetValue>(IDictionaryShape<TTargetDictionary, TTargetKey, TTargetValue> targetDictionary, object? state)
            {
                var sourceDictionary = (IDictionaryShape<TSourceDictionary, TSourceKey, TSourceValue>)state!;
                var sourceGetDictionary = sourceDictionary.GetGetDictionary();

                var keyMapper = baseVisitor.CreateMapper(sourceDictionary.KeyType, targetDictionary.KeyType);
                var valueMapper = baseVisitor.CreateMapper(sourceDictionary.ValueType, targetDictionary.ValueType);
                if (keyMapper is null || valueMapper is null)
                    return null;

                switch (targetDictionary.ConstructionStrategy)
                {
                    case CollectionConstructionStrategy.Mutable:
                        var defaultCtor = targetDictionary.GetDefaultConstructor();
                        var addEntry = targetDictionary.GetAddKeyValuePair();
                        return new Mapper<TSourceDictionary, TTargetDictionary>(source =>
                        {
                            if (source is null)
                            {
                                return default;
                            }

                            var target = defaultCtor();
                            foreach ((TSourceKey sourceKey, TSourceValue sourceValue) in sourceGetDictionary(source))
                            {
                                KeyValuePair<TTargetKey, TTargetValue> entry = new(keyMapper(sourceKey), valueMapper(sourceValue)!);
                                addEntry(ref target, entry);
                            }

                            return target;
                        });

                    case CollectionConstructionStrategy.Enumerable:
                        var createEnumerable = targetDictionary.GetEnumerableConstructor();
                        return new Mapper<TSourceDictionary, TTargetDictionary>(source =>
                        {
                            if (source is null)
                            {
                                return default;
                            }

                            return createEnumerable(sourceGetDictionary(source).Select(MapEntry));
                        });

                    case CollectionConstructionStrategy.Span:
                        var createFromSpan = targetDictionary.GetSpanConstructor();
                        return new Mapper<TSourceDictionary, TTargetDictionary>(source =>
                        {
                            if (source is null)
                            {
                                return default;
                            }

                            KeyValuePair<TTargetKey, TTargetValue>[] entries = sourceGetDictionary(source).Select(MapEntry).ToArray();
                            return createFromSpan(entries);
                        });

                    default:
                        return null;
                }

                KeyValuePair<TTargetKey, TTargetValue> MapEntry(KeyValuePair<TSourceKey, TSourceValue> entry)
                    => new(keyMapper(entry.Key), valueMapper(entry.Value)!);
            }
        }

        private Mapper<TSource, TTarget>? CacheResult<TSource, TTarget>(Mapper<TSource, TTarget>? mapper)
        {
            // Wrap cache entries in a 1-tuple to allow storing null values, which represent non-mappable types.
            _cache.Add(new Tuple<Mapper<TSource, TTarget>?>(mapper));
            return mapper;
        }

        private bool TryGetCachedResult<TSource, TTarget>(out Mapper<TSource, TTarget>? mapper)
        {
            var entry = _cache.GetOrAddDelayedValue<Tuple<Mapper<TSource, TTarget>?>>(static holder =>
                new Tuple<Mapper<TSource, TTarget>?>(left =>
                {
                    Mapper<TSource, TTarget>? mapper = holder.Value!.Item1;
                    if (mapper is null) ThrowCannotMapTypes(typeof(TSource), typeof(TTarget));
                    return mapper(left);
                }));

            mapper = entry?.Item1;
            return entry != null;
        }

        [DoesNotReturn]
        internal static void ThrowCannotMapTypes(Type left, Type right)
            => throw new InvalidOperationException($"Cannot map type '{left}' to '{right}'.");
    }
}
