using System.Diagnostics;
using TypeShape.Applications.XmlSerializer.Converters;

namespace TypeShape.Applications.XmlSerializer;

public static partial class XmlSerializer
{
    private sealed class Visitor : ITypeShapeVisitor
    {
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

        private readonly TypeCache _cache = new();

        public object? VisitType<T>(ITypeShape<T> type, object? state)
        {
            if (TryGetCachedResult<T>() is { } converter)
            {
                return converter;
            }

            switch (type.Kind)
            {
                case TypeKind.Enum:
                    converter = (XmlConverter<T>)type.GetEnumShape().Accept(this, null)!;
                    break;

                case TypeKind.Nullable:
                    converter = (XmlConverter<T>)type.GetNullableShape().Accept(this, null)!;
                    break;

                case TypeKind.Dictionary:
                    converter = (XmlConverter<T>)type.GetDictionaryShape().Accept(this, null)!;
                    break;

                case TypeKind.Enumerable:
                    converter = (XmlConverter<T>)type.GetEnumerableShape().Accept(this, null)!;
                    break;

                case TypeKind.Object:
                    XmlPropertyConverter<T>[] properties = type
                        .GetProperties()
                        .Select(prop => (XmlPropertyConverter<T>)prop.Accept(this, state)!)
                        .ToArray();

                    // Prefer the default constructor if available.
                    IConstructorShape? ctor = type
                        .GetConstructors()
                        .MinBy(ctor => ctor.ParameterCount);

                    converter = ctor != null
                         ? (XmlObjectConverter<T>)ctor.Accept(this, properties)!
                         : new XmlObjectConverter<T>(properties);

                    break;

                default:
                    Debug.Assert(type.Kind is TypeKind.None);
                    throw new NotSupportedException($"{typeof(T)}");
            }

            _cache.Add(converter);
            return converter;
        }

        public object? VisitProperty<TDeclaringType, TPropertyType>(IPropertyShape<TDeclaringType, TPropertyType> property, object? state)
        {
            XmlConverter<TPropertyType> propertyConverter = (XmlConverter<TPropertyType>)property.PropertyType.Accept(this, null)!;
            return new XmlPropertyConverter<TDeclaringType, TPropertyType>(property, propertyConverter);
        }

        public object? VisitConstructor<TDeclaringType, TArgumentState>(IConstructorShape<TDeclaringType, TArgumentState> constructor, object? state)
        {
            var properties = (XmlPropertyConverter<TDeclaringType>[])state!;

            if (constructor.ParameterCount == 0)
            {
                return new XmlObjectConverterWithDefaultCtor<TDeclaringType>(constructor.GetDefaultConstructor(), properties);
            }

            XmlPropertyConverter<TArgumentState>[] constructorParams = constructor
                .GetParameters()
                .Select(param => (XmlPropertyConverter<TArgumentState>)param.Accept(this, null)!)
                .ToArray();

            return new XmlObjectConverterWithParameterizedCtor<TDeclaringType, TArgumentState>(
                constructor.GetArgumentStateConstructor(),
                constructor.GetParameterizedConstructor(),
                constructorParams,
                properties);
        }

        public object? VisitConstructorParameter<TArgumentState, TParameterType>(IConstructorParameterShape<TArgumentState, TParameterType> parameter, object? state)
        {
            XmlConverter<TParameterType> paramConverter = (XmlConverter<TParameterType>)parameter.ParameterType.Accept(this, null)!;
            return new XmlPropertyConverter<TArgumentState, TParameterType>(parameter, paramConverter);
        }

        public object? VisitEnumerable<TEnumerable, TElement>(IEnumerableShape<TEnumerable, TElement> enumerableShape, object? state)
        {
            var elementConverter = (XmlConverter<TElement>)enumerableShape.ElementType.Accept(this, null)!;
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
            var keyConverter = (XmlConverter<TKey>)dictionaryShape.KeyType.Accept(this, null)!;
            var valueConverter = (XmlConverter<TValue>)dictionaryShape.ValueType.Accept(this, null)!;
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

        public object? VisitNullable<T>(INullableShape<T> nullableShape, object? state) where T : struct
        {
            var elementConverter = (XmlConverter<T>)nullableShape.ElementType.Accept(this, null)!;
            return new XmlNullableConverter<T>(elementConverter);
        }

        public object? VisitEnum<TEnum, TUnderlying>(IEnumShape<TEnum, TUnderlying> enumShape, object? state) where TEnum : struct, Enum
        {
            return new XmlEnumConverter<TEnum>();
        }

        private XmlConverter<T>? TryGetCachedResult<T>()
        {
            if (s_defaultConverters.TryGetValue(typeof(T), out XmlConverter? defaultConv))
            {
                return (XmlConverter<T>)defaultConv;
            }

            return _cache.GetOrAddDelayedValue<XmlConverter<T>>(static holder => new DelayedXmlConverter<T>(holder));
        }
    }
}
