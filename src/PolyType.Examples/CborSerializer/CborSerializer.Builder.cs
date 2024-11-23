using PolyType.Abstractions;
using PolyType.Examples.CborSerializer.Converters;
using PolyType.Utilities;

namespace PolyType.Examples.CborSerializer;

public static partial class CborSerializer
{
    private sealed class Builder(TypeGenerationContext generationContext) : ITypeShapeVisitor, ITypeShapeFunc
    {
        public CborConverter<T> GetOrAddConverter<T>(ITypeShape<T> typeShape) =>
            (CborConverter<T>)generationContext.GetOrAdd(typeShape, this)!;

        object? ITypeShapeFunc.Invoke<T>(ITypeShape<T> typeShape, object? shape)
        {
            // Check if the type has a built-in converter.
            if (s_builtInConverters.TryGetValue(typeof(T), out CborConverter? defaultConverter))
            {
                return (CborConverter<T>)defaultConverter;
            }

            // Otherwise, build a converter using the visitor.
            return typeShape.Accept(this);
        }

        public object? VisitObject<T>(IObjectTypeShape<T> objectShape, object? state)
        {
            CborPropertyConverter<T>[] properties = objectShape
                .GetProperties()
                .Select(prop => (CborPropertyConverter<T>)prop.Accept(this)!)
                .ToArray();
            
            IConstructorShape? ctor = objectShape.GetConstructor();
            return ctor != null
                ? (CborObjectConverter<T>)ctor.Accept(this, state: properties)!
                : new CborObjectConverter<T>(properties);
        }

        public object? VisitProperty<TDeclaringType, TPropertyType>(IPropertyShape<TDeclaringType, TPropertyType> property, object? state)
        {
            CborConverter<TPropertyType> propertyConverter = GetOrAddConverter(property.PropertyType);
            return new CborPropertyConverter<TDeclaringType, TPropertyType>(property, propertyConverter);
        }

        public object? VisitConstructor<TDeclaringType, TArgumentState>(IConstructorShape<TDeclaringType, TArgumentState> constructor, object? state)
        {
            var properties = (CborPropertyConverter<TDeclaringType>[])state!;

            if (constructor.ParameterCount == 0)
            {
                return new CborObjectConverterWithDefaultCtor<TDeclaringType>(constructor.GetDefaultConstructor(), properties);
            }

            CborPropertyConverter<TArgumentState>[] constructorParams = constructor
                .GetParameters()
                .Select(param => (CborPropertyConverter<TArgumentState>)param.Accept(this)!)
                .ToArray();

            return new CborObjectConverterWithParameterizedCtor<TDeclaringType, TArgumentState>(
                constructor.GetArgumentStateConstructor(),
                constructor.GetParameterizedConstructor(),
                constructorParams,
                properties);
        }

        public object? VisitConstructorParameter<TArgumentState, TParameterType>(IConstructorParameterShape<TArgumentState, TParameterType> parameter, object? state)
        {
            CborConverter<TParameterType> paramConverter = GetOrAddConverter(parameter.ParameterType);
            return new CborPropertyConverter<TArgumentState, TParameterType>(parameter, paramConverter);
        }

        public object? VisitEnumerable<TEnumerable, TElement>(IEnumerableTypeShape<TEnumerable, TElement> enumerableShape, object? state)
        {
            CborConverter<TElement> elementConverter = GetOrAddConverter(enumerableShape.ElementType);
            Func<TEnumerable, IEnumerable<TElement>> getEnumerable = enumerableShape.GetGetEnumerable();

            return enumerableShape.ConstructionStrategy switch
            {
                CollectionConstructionStrategy.Mutable =>
                    new CborMutableEnumerableConverter<TEnumerable, TElement>(
                        elementConverter,
                        getEnumerable,
                        enumerableShape.GetDefaultConstructor(),
                        enumerableShape.GetAddElement()),

                CollectionConstructionStrategy.Enumerable =>
                    new CborEnumerableConstructorEnumerableConverter<TEnumerable, TElement>(
                        elementConverter,
                        getEnumerable,
                        enumerableShape.GetEnumerableConstructor()),

                CollectionConstructionStrategy.Span =>
                    new CborSpanConstructorEnumerableConverter<TEnumerable, TElement>(
                        elementConverter,
                        getEnumerable,
                        enumerableShape.GetSpanConstructor()),

                _ => new CborEnumerableConverter<TEnumerable, TElement>(elementConverter, getEnumerable),
            };
        }

        public object? VisitDictionary<TDictionary, TKey, TValue>(IDictionaryTypeShape<TDictionary, TKey, TValue> dictionaryShape, object? state) where TKey : notnull
        {
            CborConverter<TKey> keyConverter = GetOrAddConverter(dictionaryShape.KeyType);
            CborConverter<TValue> valueConverter = GetOrAddConverter(dictionaryShape.ValueType);
            Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> getDictionary = dictionaryShape.GetGetDictionary();

            return dictionaryShape.ConstructionStrategy switch
            {
                CollectionConstructionStrategy.Mutable => 
                    new CborMutableDictionaryConverter<TDictionary, TKey, TValue>(
                        keyConverter,
                        valueConverter,
                        getDictionary,
                        dictionaryShape.GetDefaultConstructor(),
                        dictionaryShape.GetAddKeyValuePair()),

                CollectionConstructionStrategy.Enumerable => 
                    new CborEnumerableConstructorDictionaryConverter<TDictionary, TKey, TValue>(
                        keyConverter,
                        valueConverter,
                        getDictionary,
                        dictionaryShape.GetEnumerableConstructor()),

                CollectionConstructionStrategy.Span =>
                    new CborSpanConstructorDictionaryConverter<TDictionary, TKey, TValue>(
                        keyConverter,
                        valueConverter,
                        getDictionary,
                        dictionaryShape.GetSpanConstructor()),

                _ => new CborDictionaryConverter<TDictionary, TKey, TValue>(keyConverter, valueConverter, getDictionary),
            };
        }

        public object? VisitNullable<T>(INullableTypeShape<T> nullableShape, object? state) where T : struct
        {
            CborConverter<T> elementConverter = GetOrAddConverter(nullableShape.ElementType);
            return new CborNullableConverter<T>(elementConverter);
        }

        public object? VisitEnum<TEnum, TUnderlying>(IEnumTypeShape<TEnum, TUnderlying> enumShape, object? state) where TEnum : struct, Enum
        {
            return new CborEnumConverter<TEnum>();
        }

        private static readonly Dictionary<Type, CborConverter> s_builtInConverters = new CborConverter[]
        {
            new BoolConverter(),
            new StringConverter(),
            new SByteConverter(),
            new Int16Converter(),
            new Int32Converter(),
            new Int64Converter(),
            new Int128Converter(),
            new ByteConverter(),
            new ByteArrayConverter(),
            new UInt16Converter(),
            new UInt32Converter(),
            new UInt64Converter(),
            new UInt128Converter(),
            new CharConverter(),
            new HalfConverter(),
            new SingleConverter(),
            new DoubleConverter(),
            new DecimalConverter(),
            new DateTimeConverter(),
            new DateTimeOffsetConverter(),
            new TimeSpanConverter(),
            new DateOnlyConverter(),
            new TimeOnlyConverter(),
            new GuidConverter(),
            new UriConverter(),
            new VersionConverter(),
            new BigIntegerConverter(),
            new RuneConverter(),
            new ObjectConverter(),
        }.ToDictionary(conv => conv.Type);
    }
}
