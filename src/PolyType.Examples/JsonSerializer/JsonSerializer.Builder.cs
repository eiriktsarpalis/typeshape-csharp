using System.Diagnostics;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using PolyType.Abstractions;
using PolyType.Examples.JsonSerializer.Converters;
using PolyType.Examples.Utilities;

namespace PolyType.Examples.JsonSerializer;

public static partial class JsonSerializerTS
{
    private sealed class Builder : ITypeShapeVisitor, ITypeShapeFunc
    {
        private readonly TypeDictionary _cache = new();

        object? ITypeShapeFunc.Invoke<T>(ITypeShape<T> typeShape, object? state) => BuildJsonConverter(typeShape);

        public JsonConverter<T> BuildJsonConverter<T>(ITypeShape<T> typeShape)
        {
            if (s_defaultConverters.TryGetValue(typeShape.Type, out JsonConverter? defaultConverter))
            {
                return (JsonConverter<T>)defaultConverter;
            }

            return _cache.GetOrAdd<JsonConverter<T>>(
                typeShape, 
                this, 
                delayedValueFactory: self => new DelayedJsonConverter<T>(self));
        }

        public object? VisitObject<T>(IObjectTypeShape<T> type, object? state)
        {
            if (typeof(T) == typeof(object))
            {
                return new JsonObjectConverter(type.Provider);
            }

            JsonPropertyConverter<T>[] properties = type.GetProperties()
                .Select(prop => (JsonPropertyConverter<T>)prop.Accept(this)!)
                .ToArray();

            IConstructorShape? ctor = type.GetConstructor();
            return ctor != null
                ? (JsonObjectConverter<T>)ctor.Accept(this, state: properties)!
                : new JsonObjectConverter<T>(properties);
        }

        public object? VisitProperty<TDeclaringType, TPropertyType>(IPropertyShape<TDeclaringType, TPropertyType> property, object? state)
        {
            JsonConverter<TPropertyType> propertyConverter = BuildJsonConverter(property.PropertyType);
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
                .Select(param => (JsonPropertyConverter<TArgumentState>)param.Accept(this)!)
                .ToArray();

            return new JsonObjectConverterWithParameterizedCtor<TDeclaringType, TArgumentState>(
                constructor.GetArgumentStateConstructor(), 
                constructor.GetParameterizedConstructor(), 
                constructorParams,
                properties);
        }

        public object? VisitConstructorParameter<TArgumentState, TParameter>(IConstructorParameterShape<TArgumentState, TParameter> parameter, object? state)
        {
            JsonConverter<TParameter> paramConverter = BuildJsonConverter(parameter.ParameterType);
            return new JsonPropertyConverter<TArgumentState, TParameter>(parameter, paramConverter);
        }

        public object? VisitEnumerable<TEnumerable, TElement>(IEnumerableTypeShape<TEnumerable, TElement> enumerableShape, object? state)
        {
            JsonConverter<TElement> elementConverter = BuildJsonConverter(enumerableShape.ElementType);

            if (enumerableShape.Rank > 1)
            {
                Debug.Assert(typeof(TEnumerable).IsArray);
                return new JsonMDArrayConverter<TEnumerable, TElement>(elementConverter, enumerableShape.Rank);
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

        public object? VisitDictionary<TDictionary, TKey, TValue>(IDictionaryShape<TDictionary, TKey, TValue> dictionaryShape, object? state) where TKey : notnull
        {
            JsonConverter<TKey> keyConverter = BuildJsonConverter(dictionaryShape.KeyType);
            JsonConverter<TValue> valueConverter = BuildJsonConverter(dictionaryShape.ValueType);

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

        public object? VisitNullable<T>(INullableTypeShape<T> nullableShape, object? state) where T : struct
        {
            JsonConverter<T> elementConverter = BuildJsonConverter(nullableShape.ElementType);
            return new JsonNullableConverter<T>(elementConverter);
        }

        public object? VisitEnum<TEnum, TUnderlying>(IEnumTypeShape<TEnum, TUnderlying> enumShape, object? state) where TEnum : struct, Enum
        {
            var converter = new JsonStringEnumConverter<TEnum>();
            return converter.CreateConverter(typeof(TEnum), s_options);
        }

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
        }.ToDictionary(conv => conv.Type!);
    }
}
