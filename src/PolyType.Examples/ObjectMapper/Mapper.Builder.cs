using PolyType.Abstractions;
using PolyType.Utilities;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace PolyType.Examples.ObjectMapper;

public static partial class Mapper
{
    private delegate void PropertyMapper<TSource, TTarget>(ref TSource source, ref TTarget target);

    private sealed class Builder(TypeGenerationContext generationContext) : MapperTypeShapeVisitor, ITypeShapeFunc
    {
        public Mapper<TSource, TTarget> GetOrAddMapper<TSource, TTarget>(ITypeShape<TSource> fromShape, ITypeShape<TTarget> toShape)
        {
            ITypeShape<Mapper<TSource, TTarget>> mapperShape = new MapperShape<TSource, TTarget>(fromShape, toShape);
            return (Mapper<TSource, TTarget>)generationContext.GetOrAdd(mapperShape)!;
        }

        object? ITypeShapeFunc.Invoke<T>(ITypeShape<T> typeShape, object? state) => typeShape.Accept(this, state);

        public override object? VisitMapper<TSource, TTarget>(ITypeShape<TSource> sourceShape, ITypeShape<TTarget> targetShape, object? state)
        {
            if (sourceShape.Kind != targetShape.Kind)
            {
                // For simplicity, only map between types of matching kind.
                ThrowCannotMapTypes(typeof(TSource), typeof(TTarget));
            }

            switch (sourceShape.Kind)
            {
                case TypeShapeKind.Object:
                    var sourceObjectShape = (IObjectTypeShape<TSource>)sourceShape;
                    var targetObjectShape = (IObjectTypeShape<TTarget>)targetShape;

                    IConstructorShape? ctor = targetObjectShape.GetConstructor();

                    if (ctor is null)
                    {
                        // If TTarget is not constructible, only map if TSource is a subtype of TTarget and has no properties.
                        if (typeof(TTarget).IsAssignableFrom(typeof(TSource)) && !targetObjectShape.HasProperties)
                        {
                            return new Mapper<TSource, TSource>(source => source);

                        }    

                        ThrowCannotMapTypes(typeof(TSource), typeof(TTarget));
                    }

                    IPropertyShape[] sourceGetters = sourceObjectShape.GetProperties()
                        .Where(prop => prop.HasGetter)
                        .ToArray();

                    // Bring TSource into scope for the target ctor using a new generic visitor.
                    var visitor = new TypeScopedVisitor<TSource>(this);
                    return (Mapper<TSource, TTarget>?)ctor.Accept(visitor, state: sourceGetters);

                case TypeShapeKind.Enum:
                    return new Mapper<TSource, TTarget>(source => (TTarget)(object)source!);

                default:
                    return (Mapper<TSource, TTarget>?)sourceShape.Accept(this, state: targetShape);
            }
        }

        public override object? VisitProperty<TSource, TSourceProperty>(IPropertyShape<TSource, TSourceProperty> sourceGetter, object? state = null)
        {
            DebugExt.Assert(state is IPropertyShape or IConstructorParameterShape);
            var visitor = new PropertyScopedVisitor<TSource, TSourceProperty>(this);
            return state is IPropertyShape targetProp
                ? targetProp.Accept(visitor, sourceGetter)
                : ((IConstructorParameterShape)state).Accept(visitor, state: sourceGetter);
        }

        public override object? VisitNullable<TSource>(INullableTypeShape<TSource> nullableShape, object? state)
        {
            var targetNullable = (INullableTypeShape)state!;
            var visitor = new NullableScopedVisitor<TSource>(this);
            return targetNullable.Accept(visitor, state: nullableShape);
        }

        public override object? VisitEnumerable<TSource, TSourceElement>(IEnumerableTypeShape<TSource, TSourceElement> enumerableShape, object? state)
        {
            var targetEnumerable = (IEnumerableTypeShape)state!;
            var visitor = new EnumerableScopedVisitor<TSource, TSourceElement>(this);
            return targetEnumerable.Accept(visitor, state: enumerableShape);
        }

        public override object? VisitDictionary<TSourceDictionary, TSourceKey, TSourceValue>(IDictionaryTypeShape<TSourceDictionary, TSourceKey, TSourceValue> sourceDictionary, object? state)
        {
            var targetDictionary = (IDictionaryTypeShape)state!;
            var visitor = new DictionaryScopedVisitor<TSourceDictionary, TSourceKey, TSourceValue>(this);
            return targetDictionary.Accept(visitor, state: sourceDictionary);
        }

        private sealed class TypeScopedVisitor<TSource>(Builder baseVisitor) : TypeShapeVisitor
        {
            public override object? VisitConstructor<TTarget, TArgumentState>(IConstructorShape<TTarget, TArgumentState> targetCtor, object? state)
            {
                var sourceGetters = (IPropertyShape[])state!;
                if (targetCtor.ParameterCount == 0)
                {
                    Func<TTarget> defaultCtor = targetCtor.GetDefaultConstructor();
                    PropertyMapper<TSource, TTarget>[] propertyMappers = targetCtor.DeclaringType.GetProperties()
                        .Where(prop => prop.HasSetter)
                        .Select(setter =>
                            sourceGetters.FirstOrDefault(getter => getter.Name == setter.Name) is { } getter
                            ? (PropertyMapper<TSource, TTarget>?)getter.Accept(baseVisitor, state: setter)
                            : null)
                        .Where(mapper => mapper != null)
                        .ToArray()!;

                    return new Mapper<TSource, TTarget>(source =>
                    {
                        if (source is null)
                        {
                            return default;
                        }

                        TTarget target = defaultCtor();
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
                                ? (PropertyMapper<TSource, TArgumentState>?)getter.Accept(baseVisitor, state: targetParam)
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

                        TArgumentState argumentState = argumentStateCtor();
                        foreach (PropertyMapper<TSource, TArgumentState> mapper in propertyMappers)
                        {
                            mapper(ref source, ref argumentState);
                        }

                        return ctor(ref argumentState);
                    });
                }
            }
        }

        private sealed class PropertyScopedVisitor<TSource, TSourceProperty>(Builder baseVisitor) : TypeShapeVisitor
        {
            public override object? VisitProperty<TTarget, TTargetProperty>(IPropertyShape<TTarget, TTargetProperty> targetProperty, object? state)
            {
                IPropertyShape<TSource, TSourceProperty> sourceProperty = (IPropertyShape<TSource, TSourceProperty>)state!;
                var propertyTypeMapper = baseVisitor.GetOrAddMapper(sourceProperty.PropertyType, targetProperty.PropertyType);

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
                var propertyTypeMapper = baseVisitor.GetOrAddMapper(sourceProperty.PropertyType, targetParameter.ParameterType);
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

        private sealed class NullableScopedVisitor<TSourceElement>(Builder baseVisitor) : TypeShapeVisitor
            where TSourceElement : struct
        {
            public override object? VisitNullable<TTargetElement>(INullableTypeShape<TTargetElement> nullableShape, object? state)
                where TTargetElement : struct
            {
                var sourceNullable = (INullableTypeShape<TSourceElement>)state!;
                var elementMapper = baseVisitor.GetOrAddMapper(sourceNullable.ElementType, nullableShape.ElementType);
                return new Mapper<TSourceElement?, TTargetElement?>(source => source is null ? null : elementMapper(source.Value));
            }
        }

        private sealed class EnumerableScopedVisitor<TSourceEnumerable, TSourceElement>(Builder baseVisitor) : TypeShapeVisitor
        {
            public override object? VisitEnumerable<TTargetEnumerable, TTargetElement>(IEnumerableTypeShape<TTargetEnumerable, TTargetElement> enumerableShape, object? state)
            {
                var sourceEnumerable = (IEnumerableTypeShape<TSourceEnumerable, TSourceElement>)state!;
                var sourceGetEnumerable = sourceEnumerable.GetGetEnumerable();
                var elementMapper = baseVisitor.GetOrAddMapper(sourceEnumerable.ElementType, enumerableShape.ElementType);

                switch (enumerableShape.ConstructionStrategy)
                {
                    case CollectionConstructionStrategy.Mutable:
                        var defaultCtor = enumerableShape.GetDefaultConstructor();
                        var addElement = enumerableShape.GetAddElement();
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
                        var createEnumerable = enumerableShape.GetEnumerableConstructor();
                        return new Mapper<TSourceEnumerable, TTargetEnumerable>(source =>
                        {
                            if (source is null)
                            {
                                return default;
                            }

                            return createEnumerable(sourceGetEnumerable(source).Select(e => elementMapper(e!)));
                        });

                    case CollectionConstructionStrategy.Span:
                        var createSpan = enumerableShape.GetSpanConstructor();
                        return new Mapper<TSourceEnumerable, TTargetEnumerable>(source =>
                        {
                            if (source is null)
                            {
                                return default;
                            }

                            return createSpan(sourceGetEnumerable(source).Select(e => elementMapper(e!)).ToArray());
                        });

                    default:
                        ThrowCannotMapTypes(typeof(TSourceEnumerable), typeof(TTargetEnumerable));
                        return null;
                }       
            }
        }

        private sealed class DictionaryScopedVisitor<TSourceDictionary, TSourceKey, TSourceValue>(Builder baseVisitor) : TypeShapeVisitor
            where TSourceKey : notnull
        {
            public override object? VisitDictionary<TTargetDictionary, TTargetKey, TTargetValue>(IDictionaryTypeShape<TTargetDictionary, TTargetKey, TTargetValue> targetDictionary, object? state)
            {
                var sourceDictionary = (IDictionaryTypeShape<TSourceDictionary, TSourceKey, TSourceValue>)state!;
                var sourceGetDictionary = sourceDictionary.GetGetDictionary();
                var keyMapper = baseVisitor.GetOrAddMapper(sourceDictionary.KeyType, targetDictionary.KeyType);
                var valueMapper = baseVisitor.GetOrAddMapper(sourceDictionary.ValueType, targetDictionary.ValueType);

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
                            foreach (var sourceEntry in sourceGetDictionary(source))
                            {
                                KeyValuePair<TTargetKey, TTargetValue> entry = new(keyMapper(sourceEntry.Key), valueMapper(sourceEntry.Value)!);
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
                        ThrowCannotMapTypes(typeof(TSourceDictionary), typeof(TTargetDictionary));
                        return null;
                }

                KeyValuePair<TTargetKey, TTargetValue> MapEntry(KeyValuePair<TSourceKey, TSourceValue> entry)
                    => new(keyMapper(entry.Key), valueMapper(entry.Value)!);
            }
        }

        [DoesNotReturn]
        internal static void ThrowCannotMapTypes(Type left, Type right)
            => throw new InvalidOperationException($"Cannot map type '{left}' to '{right}'.");
    }

    // Defines a synthetic type shape representing pairs of types.
    private sealed class MapperShape<TSource, TTarget>(ITypeShape<TSource> source, ITypeShape<TTarget> target) : ITypeShape<Mapper<TSource, TTarget>>
    {
        public TypeShapeKind Kind => (TypeShapeKind)101;
        public ITypeShapeProvider Provider => source.Provider;
        public Type Type => typeof(Mapper<TSource, TTarget>);
        public ICustomAttributeProvider? AttributeProvider => typeof(Mapper<TSource, TTarget>);
        public object? Accept(ITypeShapeVisitor visitor, object? state = null) => ((MapperTypeShapeVisitor)visitor).VisitMapper(source, target, state);
        public object? Invoke(ITypeShapeFunc func, object? state = null) => func.Invoke(this, state);
    }

    private abstract class MapperTypeShapeVisitor : TypeShapeVisitor
    {
        public abstract object? VisitMapper<TSource, TTarget>(ITypeShape<TSource> source, ITypeShape<TTarget> target, object? state);
    }

    private sealed class DelayedMapperFactory : MapperTypeShapeVisitor, IDelayedValueFactory
    {
        public DelayedValue Create<T>(ITypeShape<T> typeShape) => (DelayedValue)typeShape.Accept(this)!;
        public override object? VisitMapper<TSource, TTarget>(ITypeShape<TSource> left, ITypeShape<TTarget> right, object? state)
        {
            return new DelayedValue<Mapper<TSource, TTarget>>(self => left => self.Result(left));
        }
    }
}
