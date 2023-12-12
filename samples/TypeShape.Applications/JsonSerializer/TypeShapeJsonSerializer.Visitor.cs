namespace TypeShape.Applications.JsonSerializer;

using System.Diagnostics;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using TypeShape.Applications.JsonSerializer.Converters;

public static partial class TypeShapeJsonSerializer
{
    private sealed class Visitor : ITypeShapeVisitor
    {
        private static readonly Dictionary<Type, JsonConverter> s_defaultConverters = new JsonConverter[]
        {
            JsonMetadataServices.BooleanConverter,
            JsonMetadataServices.SByteConverter,
            JsonMetadataServices.Int16Converter,
            JsonMetadataServices.Int32Converter,
            JsonMetadataServices.Int64Converter,
            JsonMetadataServices.Int128Converter,
            JsonMetadataServices.ByteConverter,
            JsonMetadataServices.ByteArrayConverter,
            JsonMetadataServices.UInt16Converter,
            JsonMetadataServices.UInt32Converter,
            JsonMetadataServices.UInt64Converter,
            JsonMetadataServices.UInt128Converter,
            JsonMetadataServices.CharConverter,
            JsonMetadataServices.StringConverter,
            JsonMetadataServices.HalfConverter,
            JsonMetadataServices.SingleConverter,
            JsonMetadataServices.DoubleConverter,
            JsonMetadataServices.DecimalConverter,
            JsonMetadataServices.DateTimeConverter,
            JsonMetadataServices.DateTimeOffsetConverter,
            JsonMetadataServices.TimeSpanConverter,
            JsonMetadataServices.DateOnlyConverter,
            JsonMetadataServices.TimeOnlyConverter,
            JsonMetadataServices.GuidConverter,
            JsonMetadataServices.UriConverter,
            JsonMetadataServices.VersionConverter,
            new BigIntegerConverter(),
            new RuneConverter(),
            new JsonObjectConverter(),
        }.ToDictionary(conv => conv.Type!);

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
                    converter = (JsonConverter<T>)type.GetEnumShape().Accept(this, null)!;
                    break;

                case TypeKind.Nullable:
                    converter = (JsonConverter<T>)type.GetNullableShape().Accept(this, null)!;
                    break;

                case TypeKind.Dictionary:
                    converter = (JsonConverter<T>)type.GetDictionaryShape().Accept(this, null)!;
                    break;

                case TypeKind.Enumerable:
                    converter = (JsonConverter<T>)type.GetEnumerableShape().Accept(this, null)!;
                    break;

                default:
                    Debug.Assert(type.Kind is TypeKind.None or TypeKind.Object);

                    JsonPropertyConverter<T>[] properties = type
                        .GetProperties(includeFields: true)
                        .Select(prop => (JsonPropertyConverter<T>)prop.Accept(this, state)!)
                        .ToArray();

                    // Prefer the default constructor if available.
                    IConstructorShape? ctor = type
                        .GetConstructors(includeProperties: true, includeFields: true)
                        .MinBy(ctor => ctor.ParameterCount);

                   converter = ctor != null
                        ? (JsonObjectConverter<T>)ctor.Accept(this, properties)!
                        : new JsonObjectConverter<T>(properties);

                    break;
            }

            _cache.Add(converter);
            return converter;
        }

        public object? VisitProperty<TDeclaringType, TPropertyType>(IPropertyShape<TDeclaringType, TPropertyType> property, object? state)
        {
            JsonConverter<TPropertyType> propertyConverter = (JsonConverter<TPropertyType>)property.PropertyType.Accept(this, null)!;
            return new JsonPropertyConverter<TDeclaringType, TPropertyType>(property, propertyConverter);
        }

        public object? VisitConstructor<TDeclaringType, TArgumentState>(IConstructorShape<TDeclaringType, TArgumentState> constructor, object? state)
        {
            var properties = (JsonPropertyConverter<TDeclaringType>[])state!;

            if (constructor.ParameterCount == 0)
            {
                return new JsonObjectConverterWithDefaultCtor<TDeclaringType>(constructor.GetDefaultConstructor(), properties);
            }

            JsonPropertyConverter<TArgumentState>[] constructorParams = constructor
                .GetParameters()
                .Select(param => (JsonPropertyConverter<TArgumentState>)param.Accept(this, null)!)
                .ToArray();

            return new JsonObjectConverterWithParameterizedCtor<TDeclaringType, TArgumentState>(
                constructor.GetArgumentStateConstructor(), 
                constructor.GetParameterizedConstructor(), 
                constructorParams,
                properties);
        }

        public object? VisitConstructorParameter<TArgumentState, TParameter>(IConstructorParameterShape<TArgumentState, TParameter> parameter, object? state)
        {
            JsonConverter<TParameter> paramConverter = (JsonConverter<TParameter>)parameter.ParameterType.Accept(this, null)!;
            return new JsonPropertyConverter<TArgumentState, TParameter>(parameter, paramConverter);
        }

        public object? VisitEnumerable<TEnumerable, TElement>(IEnumerableShape<TEnumerable, TElement> enumerableShape, object? state)
        {
            var elementConverter = (JsonConverter<TElement>)enumerableShape.ElementType.Accept(this, null)!;

            if (enumerableShape.Rank > 1)
            {
                Debug.Assert(typeof(TEnumerable).IsArray);
                return enumerableShape.Rank switch
                {
                    2 => new Json2DArrayConverter<TElement>(elementConverter),
                    _ => throw new NotImplementedException("Array rank > 2 not implemented."),
                };
            }

            return enumerableShape.ConstructionStrategy switch
            {
                CollectionConstructionStrategy.Mutable => 
                    new JsonMutableEnumerableConverter<TEnumerable, TElement>(
                        elementConverter,
                        enumerableShape,
                        enumerableShape.GetDefaultConstructor(),
                        enumerableShape.GetAddElement()),

                CollectionConstructionStrategy.Enumerable => 
                    new JsonEnumerableConstructorEnumerableConverter<TEnumerable, TElement>(
                        elementConverter,
                        enumerableShape,
                        enumerableShape.GetEnumerableConstructor()),

                CollectionConstructionStrategy.Span => 
                    new JsonSpanConstructorEnumerableConverter<TEnumerable, TElement>(
                        elementConverter,
                        enumerableShape,
                        enumerableShape.GetSpanConstructor()),
                _ => new JsonEnumerableConverter<TEnumerable, TElement>(elementConverter, enumerableShape),
            };
        }

        public object? VisitDictionary<TDictionary, TKey, TValue>(IDictionaryShape<TDictionary, TKey, TValue> dictionaryShape, object? state)
            where TKey : notnull
        {
            var keyConverter = (JsonConverter<TKey>)dictionaryShape.KeyType.Accept(this, null)!;
            var valueConverter = (JsonConverter<TValue>)dictionaryShape.ValueType.Accept(this, null)!;

            return dictionaryShape.ConstructionStrategy switch
            {
                CollectionConstructionStrategy.Mutable => 
                    new JsonMutableDictionaryConverter<TDictionary, TKey, TValue>(
                        keyConverter,
                        valueConverter,
                        dictionaryShape,
                        dictionaryShape.GetDefaultConstructor(),
                        dictionaryShape.GetAddKeyValuePair()),

                CollectionConstructionStrategy.Enumerable => 
                    new JsonEnumerableConstructorDictionaryConverter<TDictionary, TKey, TValue>(
                        keyConverter,
                        valueConverter,
                        dictionaryShape,
                        dictionaryShape.GetEnumerableConstructor()),

                CollectionConstructionStrategy.Span => 
                    new JsonSpanConstructorDictionaryConverter<TDictionary, TKey, TValue>(
                        keyConverter,
                        valueConverter,
                        dictionaryShape,
                        dictionaryShape.GetSpanConstructor()),

                _ => new JsonDictionaryConverter<TDictionary, TKey, TValue>(keyConverter, valueConverter, dictionaryShape),
            };
        }

        public object? VisitNullable<T>(INullableShape<T> nullableShape, object? state) where T : struct
        {
            var elementConverter = (JsonConverter<T>)nullableShape.ElementType.Accept(this, null)!;
            return new JsonNullableConverter<T>(elementConverter);
        }

        public object? VisitEnum<TEnum, TUnderlying>(IEnumShape<TEnum, TUnderlying> enumShape, object? state) where TEnum : struct, Enum
        {
            var converter = new JsonStringEnumConverter<TEnum>();
            return converter.CreateConverter(typeof(TEnum), s_options);
        }

        private JsonConverter<T>? TryGetCachedResult<T>()
        {
            if (s_defaultConverters.TryGetValue(typeof(T), out JsonConverter? defaultConv))
            {
                return (JsonConverter<T>)defaultConv;
            }

            return _cache.GetOrAddDelayedValue<JsonConverter<T>>(static holder => new DelayedJsonConverter<T>(holder));
        }
    }
}
