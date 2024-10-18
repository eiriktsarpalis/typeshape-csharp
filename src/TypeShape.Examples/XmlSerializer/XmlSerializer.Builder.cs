using TypeShape.Examples.XmlSerializer.Converters;
using TypeShape.Abstractions;
using TypeShape.Examples.Utilities;

namespace TypeShape.Examples.XmlSerializer;

public static partial class XmlSerializer
{
    private sealed class Builder : ITypeShapeVisitor
    {
        private readonly TypeDictionary _cache = new();

        public XmlConverter<T> BuildConverter<T>(ITypeShape<T> shape)
        {
            if (s_defaultConverters.TryGetValue(typeof(T), out XmlConverter? defaultConverter))
            {
                return (XmlConverter<T>)defaultConverter;
            }

            return _cache.GetOrAdd<XmlConverter<T>>(shape, this, delayedValueFactory: self => new DelayedXmlConverter<T>(self));
        }

        public object? VisitObject<T>(IObjectTypeShape<T> type, object? state)
        {
            XmlPropertyConverter<T>[] properties = type.GetProperties()
                .Select(prop => (XmlPropertyConverter<T>)prop.Accept(this)!)
                .ToArray();

            // Prefer the default constructor if available.
            IConstructorShape? ctor = type.GetConstructor();
            return ctor != null
                ? (XmlObjectConverter<T>)ctor.Accept(this, state: properties)!
                : new XmlObjectConverter<T>(properties);
        }

        public object? VisitProperty<TDeclaringType, TPropertyType>(IPropertyShape<TDeclaringType, TPropertyType> property, object? state)
        {
            XmlConverter<TPropertyType> propertyConverter = BuildConverter(property.PropertyType);
            return new XmlPropertyConverter<TDeclaringType, TPropertyType>(property, propertyConverter);
        }

        public object? VisitConstructor<TDeclaringType, TArgumentState>(IConstructorShape<TDeclaringType, TArgumentState> constructor, object? state)
        {
            var properties = (XmlPropertyConverter<TDeclaringType>[])state!;

            if (constructor.ParameterCount == 0)
            {
                return new XmlObjectConverterWithDefaultCtor<TDeclaringType>(constructor.GetDefaultConstructor(), properties);
            }

            XmlPropertyConverter<TArgumentState>[] constructorParams = constructor.GetParameters()
                .Select(param => (XmlPropertyConverter<TArgumentState>)param.Accept(this)!)
                .ToArray();

            return new XmlObjectConverterWithParameterizedCtor<TDeclaringType, TArgumentState>(
                constructor.GetArgumentStateConstructor(),
                constructor.GetParameterizedConstructor(),
                constructorParams,
                properties);
        }

        public object? VisitConstructorParameter<TArgumentState, TParameterType>(IConstructorParameterShape<TArgumentState, TParameterType> parameter, object? state)
        {
            XmlConverter<TParameterType> paramConverter = BuildConverter(parameter.ParameterType);
            return new XmlPropertyConverter<TArgumentState, TParameterType>(parameter, paramConverter);
        }

        public object? VisitEnumerable<TEnumerable, TElement>(IEnumerableTypeShape<TEnumerable, TElement> enumerableShape, object? state)
        {
            XmlConverter<TElement> elementConverter = BuildConverter(enumerableShape.ElementType);
            Func<TEnumerable, IEnumerable<TElement>> getEnumerable = enumerableShape.GetGetEnumerable();

            return enumerableShape.ConstructionStrategy switch
            {
                CollectionConstructionStrategy.Mutable => 
                    new XmlMutableEnumerableConverter<TEnumerable, TElement>(
                        elementConverter,
                        getEnumerable,
                        enumerableShape.GetDefaultConstructor(),
                        enumerableShape.GetAddElement()),
                CollectionConstructionStrategy.Enumerable => 
                    new XmlEnumerableConstructorEnumerableConverter<TEnumerable, TElement>(
                        elementConverter,
                        getEnumerable,
                        enumerableShape.GetEnumerableConstructor()),
                CollectionConstructionStrategy.Span => 
                    new XmlSpanConstructorEnumerableConverter<TEnumerable, TElement>(
                        elementConverter,
                        getEnumerable,
                        enumerableShape.GetSpanConstructor()),
                _ => new XmlEnumerableConverter<TEnumerable, TElement>(elementConverter, getEnumerable),
            };
        }

        public object? VisitDictionary<TDictionary, TKey, TValue>(IDictionaryShape<TDictionary, TKey, TValue> dictionaryShape, object? state) where TKey : notnull
        {
            XmlConverter<TKey> keyConverter = BuildConverter(dictionaryShape.KeyType);
            XmlConverter<TValue> valueConverter = BuildConverter(dictionaryShape.ValueType);
            Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> getEnumerable = dictionaryShape.GetGetDictionary();

            return dictionaryShape.ConstructionStrategy switch
            {
                CollectionConstructionStrategy.Mutable => 
                    new XmlMutableDictionaryConverter<TDictionary, TKey, TValue>(
                        keyConverter,
                        valueConverter,
                        getEnumerable,
                        dictionaryShape.GetDefaultConstructor(),
                        dictionaryShape.GetAddKeyValuePair()),

                CollectionConstructionStrategy.Enumerable => 
                    new XmlEnumerableConstructorDictionaryConverter<TDictionary, TKey, TValue>(
                        keyConverter,
                        valueConverter,
                        getEnumerable,
                        dictionaryShape.GetEnumerableConstructor()),

                CollectionConstructionStrategy.Span => 
                    new XmlSpanConstructorDictionaryConverter<TDictionary, TKey, TValue>(
                        keyConverter,
                        valueConverter,
                        getEnumerable,
                        dictionaryShape.GetSpanConstructor()),

                _ => new XmlDictionaryConverter<TDictionary, TKey, TValue>(keyConverter, valueConverter, getEnumerable),
            };
        }

        public object? VisitNullable<T>(INullableTypeShape<T> nullableShape, object? state) where T : struct
        {
            XmlConverter<T> elementConverter = BuildConverter(nullableShape.ElementType);
            return new XmlNullableConverter<T>(elementConverter);
        }

        public object? VisitEnum<TEnum, TUnderlying>(IEnumTypeShape<TEnum, TUnderlying> enumShape, object? state) where TEnum : struct, Enum
        {
            return new XmlEnumConverter<TEnum>();
        }

        private static readonly Dictionary<Type, XmlConverter> s_defaultConverters = new XmlConverter[]
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
            new BigIntegerConverter(),
            new RuneConverter(),
            new UriConverter(),
            new VersionConverter(),
            new ObjectConverter(),
        }.ToDictionary(conv => conv.Type);
    }
}
