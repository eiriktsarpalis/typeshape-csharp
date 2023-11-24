using System.Diagnostics;
using TypeShape.Applications.CborSerializer.Converters;

namespace TypeShape.Applications.CborSerializer;

public static partial class CborSerializer
{
    private sealed class Visitor : ITypeShapeVisitor
    {
        private static readonly Dictionary<Type, CborConverter> s_defaultConverters = new CborConverter[]
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
                    converter = (CborConverter<T>)type.GetEnumShape().Accept(this, null)!;
                    break;

                case TypeKind.Nullable:
                    converter = (CborConverter<T>)type.GetNullableShape().Accept(this, null)!;
                    break;

                case TypeKind.Dictionary:
                    converter = (CborConverter<T>)type.GetDictionaryShape().Accept(this, null)!;
                    break;

                case TypeKind.Enumerable:
                    converter = (CborConverter<T>)type.GetEnumerableShape().Accept(this, null)!;
                    break;

                default:
                    Debug.Assert(type.Kind == TypeKind.None);

                    CborPropertyConverter<T>[] properties = type
                        .GetProperties(nonPublic: false, includeFields: true)
                        .Select(prop => (CborPropertyConverter<T>)prop.Accept(this, state)!)
                        .ToArray();

                    IConstructorShape? ctor = type
                        .GetConstructors(nonPublic: false)
                        .OrderByDescending(ctor => ctor.ParameterCount)
                        .FirstOrDefault();

                    converter = ctor != null
                         ? (CborObjectConverter<T>)ctor.Accept(this, properties)!
                         : new CborObjectConverter<T>(properties);

                    break;
            }

            _cache.Add(converter);
            return converter;
        }

        public object? VisitProperty<TDeclaringType, TPropertyType>(IPropertyShape<TDeclaringType, TPropertyType> property, object? state)
        {
            CborConverter<TPropertyType> propertyConverter = (CborConverter<TPropertyType>)property.PropertyType.Accept(this, null)!;
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
                .Select(param => (CborPropertyConverter<TArgumentState>)param.Accept(this, null)!)
                .ToArray();

            return new CborObjectConverterWithParameterizedCtor<TDeclaringType, TArgumentState>(
                constructor.GetArgumentStateConstructor(),
                constructor.GetParameterizedConstructor(),
                constructorParams,
                properties);
        }

        public object? VisitConstructorParameter<TArgumentState, TParameterType>(IConstructorParameterShape<TArgumentState, TParameterType> parameter, object? state)
        {
            CborConverter<TParameterType> paramConverter = (CborConverter<TParameterType>)parameter.ParameterType.Accept(this, null)!;
            return new CborPropertyConverter<TArgumentState, TParameterType>(parameter, paramConverter);
        }

        public object? VisitEnumerable<TEnumerable, TElement>(IEnumerableShape<TEnumerable, TElement> enumerableShape, object? state)
        {
            var elementConverter = (CborConverter<TElement>)enumerableShape.ElementType.Accept(this, null)!;
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

        public object? VisitDictionary<TDictionary, TKey, TValue>(IDictionaryShape<TDictionary, TKey, TValue> dictionaryShape, object? state) where TKey : notnull
        {
            var keyConverter = (CborConverter<TKey>)dictionaryShape.KeyType.Accept(this, null)!;
            var valueConverter = (CborConverter<TValue>)dictionaryShape.ValueType.Accept(this, null)!;
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

        public object? VisitNullable<T>(INullableShape<T> nullableShape, object? state) where T : struct
        {
            var elementConverter = (CborConverter<T>)nullableShape.ElementType.Accept(this, null)!;
            return new CborNullableConverter<T>(elementConverter);
        }

        public object? VisitEnum<TEnum, TUnderlying>(IEnumShape<TEnum, TUnderlying> enumShape, object? state) where TEnum : struct, Enum
        {
            return new CborEnumConverter<TEnum>();
        }

        private CborConverter<T>? TryGetCachedResult<T>()
        {
            if (s_defaultConverters.TryGetValue(typeof(T), out CborConverter? defaultConv))
            {
                return (CborConverter<T>)defaultConv;
            }

            return _cache.GetOrAddDelayedValue<CborConverter<T>>(static holder => new DelayedCborConverter<T>(holder));
        }
    }
}
