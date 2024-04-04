﻿namespace TypeShape.Applications.JsonSerializer;

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using TypeShape.Applications.JsonSerializer.Converters;

public static partial class TypeShapeJsonSerializer
{
    private sealed class Builder(JsonSerializerOptions options) : ITypeShapeVisitor
    {
        private readonly TypeDictionary _cache = new();

        public JsonConverter<T> BuildConverter<T>(ITypeShape<T> typeShape)
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

        public object? VisitType<T>(ITypeShape<T> type, object? state)
        {
            JsonPropertyConverter<T>[] properties = type
                .GetProperties()
                .Select(prop => (JsonPropertyConverter<T>)prop.Accept(this, state)!)
                .ToArray();

            // Prefer the default constructor if available.
            IConstructorShape? ctor = type
                .GetConstructors()
                .MinBy(ctor => ctor.ParameterCount);

            return ctor != null
                ? (JsonObjectConverter<T>)ctor.Accept(this, state: properties)!
                : new JsonObjectConverter<T>(properties);
        }

        public object? VisitProperty<TDeclaringType, TPropertyType>(IPropertyShape<TDeclaringType, TPropertyType> property, object? state)
        {
            JsonConverter<TPropertyType> propertyConverter = BuildConverter(property.PropertyType);
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
            JsonConverter<TParameter> paramConverter = BuildConverter(parameter.ParameterType);
            return new JsonPropertyConverter<TArgumentState, TParameter>(parameter, paramConverter);
        }

        public object? VisitEnumerable<TEnumerable, TElement>(IEnumerableTypeShape<TEnumerable, TElement> enumerableTypeShape, object? state)
        {
            JsonConverter<TElement> elementConverter = BuildConverter(enumerableTypeShape.ElementType);

            if (enumerableTypeShape.Rank > 1)
            {
                Debug.Assert(typeof(TEnumerable).IsArray);
                return enumerableTypeShape.Rank switch
                {
                    2 => new Json2DArrayConverter<TElement>(elementConverter),
                    _ => throw new NotImplementedException("Array rank > 2 not implemented."),
                };
            }

            return enumerableTypeShape.ConstructionStrategy switch
            {
                CollectionConstructionStrategy.Mutable => 
                    new JsonMutableEnumerableConverter<TEnumerable, TElement>(
                        elementConverter,
                        enumerableTypeShape,
                        enumerableTypeShape.GetDefaultConstructor(),
                        enumerableTypeShape.GetAddElement()),

                CollectionConstructionStrategy.Enumerable => 
                    new JsonEnumerableConstructorEnumerableConverter<TEnumerable, TElement>(
                        elementConverter,
                        enumerableTypeShape,
                        enumerableTypeShape.GetEnumerableConstructor()),

                CollectionConstructionStrategy.Span => 
                    new JsonSpanConstructorEnumerableConverter<TEnumerable, TElement>(
                        elementConverter,
                        enumerableTypeShape,
                        enumerableTypeShape.GetSpanConstructor()),
                _ => new JsonEnumerableConverter<TEnumerable, TElement>(elementConverter, enumerableTypeShape),
            };
        }

        public object? VisitDictionary<TDictionary, TKey, TValue>(IDictionaryShape<TDictionary, TKey, TValue> dictionaryShape, object? state) where TKey : notnull
        {
            JsonConverter<TKey> keyConverter = BuildConverter(dictionaryShape.KeyType);
            JsonConverter<TValue> valueConverter = BuildConverter(dictionaryShape.ValueType);

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

        public object? VisitNullable<T>(INullableTypeShape<T> nullableTypeShape, object? state) where T : struct
        {
            JsonConverter<T> elementConverter = BuildConverter(nullableTypeShape.ElementType);
            return new JsonNullableConverter<T>(elementConverter);
        }

        public object? VisitEnum<TEnum, TUnderlying>(IEnumTypeShape<TEnum, TUnderlying> enumTypeShape, object? state) where TEnum : struct, Enum
        {
            var converter = new JsonStringEnumConverter<TEnum>();
            return converter.CreateConverter(typeof(TEnum), options);
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
            new JsonObjectConverter(),
        }.ToDictionary(conv => conv.Type!);
    }
}