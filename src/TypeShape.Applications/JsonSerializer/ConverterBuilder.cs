namespace TypeShape.Applications.JsonSerializer;

using System.Diagnostics;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using TypeShape.Applications.JsonSerializer.Converters;

public static partial class ConverterBuilder
{
    public static JsonConverter<T> Create<T>(ITypeShape<T> shape)
    {
        var visitor = new Visitor();
        return (JsonConverter<T>)shape.Accept(visitor, null)!;
    }

    private sealed class Visitor : ITypeShapeVisitor
    {
        private static readonly Dictionary<Type, JsonConverter> s_defaultConverters = new JsonConverter[]
        {
            JsonMetadataServices.BooleanConverter,
            JsonMetadataServices.SByteConverter,
            JsonMetadataServices.Int16Converter,
            JsonMetadataServices.Int32Converter,
            JsonMetadataServices.Int64Converter,
            new Int128Converter(),
            JsonMetadataServices.ByteConverter,
            JsonMetadataServices.ByteArrayConverter,
            JsonMetadataServices.UInt16Converter,
            JsonMetadataServices.UInt32Converter,
            JsonMetadataServices.UInt64Converter,
            new UInt128Converter(),
            JsonMetadataServices.CharConverter,
            JsonMetadataServices.StringConverter,
            new HalfConverter(),
            JsonMetadataServices.SingleConverter,
            JsonMetadataServices.DoubleConverter,
            JsonMetadataServices.DecimalConverter,
            JsonMetadataServices.DateTimeConverter,
            JsonMetadataServices.TimeSpanConverter,
            JsonMetadataServices.DateOnlyConverter,
            JsonMetadataServices.TimeOnlyConverter,
            JsonMetadataServices.GuidConverter,
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

                case TypeKind kind when ((kind & TypeKind.Dictionary) != 0):
                    converter = (JsonConverter<T>)type.GetDictionaryShape().Accept(this, null)!;
                    break;

                case TypeKind.Enumerable:
                    converter = (JsonConverter<T>)type.GetEnumerableShape().Accept(this, null)!;
                    break;

                default:
                    Debug.Assert(type.Kind == TypeKind.None);

                    JsonPropertyConverter<T>[] properties = type
                        .GetProperties(nonPublic: false, includeFields: true)
                        .Select(prop => (JsonPropertyConverter<T>)prop.Accept(this, state)!)
                        .ToArray();

                    IConstructorShape? ctor = type
                        .GetConstructors(nonPublic: false)
                        .OrderByDescending(ctor => ctor.ParameterCount)
                        .FirstOrDefault();

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

            IConstructorShape<TEnumerable>? constructor = enumerableShape.Type.GetConstructors(nonPublic: false)
                .Where(ctor =>
                    (ctor.ParameterCount == 0 && enumerableShape.IsMutable) ||
                    (ctor.ParameterCount == 1 && ctor.GetParameters().First().ParameterType.Type == typeof(IEnumerable<TElement>)))
                .OrderBy(ctor => ctor.ParameterCount)
                .OfType<IConstructorShape<TEnumerable>>()
                .FirstOrDefault();

            switch (constructor)
            {
                case { ParameterCount: 0 }:
                    Debug.Assert(enumerableShape.IsMutable);
                    return new JsonMutableEnumerableConverter<TEnumerable, TElement>(
                        elementConverter,
                        enumerableShape, 
                        constructor.GetDefaultConstructor(), 
                        enumerableShape.GetAddElement()
                    );

                case IConstructorShape<TEnumerable, IEnumerable<TElement>> enumerableCtor:
                    Debug.Assert(constructor.ParameterCount == 1);

                    return new JsonImmutableEnumerableConverter<TEnumerable, TElement>(
                        elementConverter,
                        enumerableShape,
                        enumerableCtor.GetParameterizedConstructor()
                    );

                default:
                    Debug.Assert(constructor is null);
                    return new JsonEnumerableConverter<TEnumerable, TElement>(elementConverter, enumerableShape);
            }
        }

        public object? VisitDictionary<TDictionary, TKey, TValue>(IDictionaryShape<TDictionary, TKey, TValue> dictionaryShape, object? state)
            where TKey : notnull
        {
            var keyConverter = (JsonConverter<TKey>)dictionaryShape.KeyType.Accept(this, null)!;
            var valueConverter = (JsonConverter<TValue>)dictionaryShape.ValueType.Accept(this, null)!;

            IConstructorShape<TDictionary>? constructor = dictionaryShape.Type.GetConstructors(nonPublic: false)
                .Where(ctor =>
                    (ctor.ParameterCount == 0 && dictionaryShape.IsMutable) ||
                    (ctor.ParameterCount == 1 && ctor.GetParameters().First().ParameterType.Type == typeof(IEnumerable<KeyValuePair<TKey, TValue>>)))
                .OrderBy(ctor => ctor.ParameterCount)
                .OfType<IConstructorShape<TDictionary>>()
                .FirstOrDefault();

            switch (constructor)
            {
                case { ParameterCount: 0 }:
                    Debug.Assert(dictionaryShape.IsMutable);
                    return new JsonMutableDictionaryConverter<TDictionary, TKey, TValue>(
                        keyConverter,
                        valueConverter,
                        dictionaryShape,
                        constructor.GetDefaultConstructor(),
                        dictionaryShape.GetAddKeyValuePair());

                case IConstructorShape<TDictionary, IEnumerable<KeyValuePair<TKey, TValue>>> enumerableCtor:
                    Debug.Assert(constructor.ParameterCount == 1);
                    return new JsonImmutableDictionaryConverter<TDictionary, TKey, TValue>(
                        keyConverter,
                        valueConverter,
                        dictionaryShape,
                        enumerableCtor.GetParameterizedConstructor());

                default:
                    Debug.Assert(constructor is null);
                    return new JsonDictionaryConverter<TDictionary, TKey, TValue>(keyConverter, valueConverter, dictionaryShape);
            }
        }

        public object? VisitNullable<T>(INullableShape<T> nullableShape, object? state) where T : struct
        {
            var elementConverter = (JsonConverter<T>)nullableShape.ElementType.Accept(this, null)!;
            return new JsonNullableConverter<T>(elementConverter);
        }

        public object? VisitEnum<TEnum, TUnderlying>(IEnumShape<TEnum, TUnderlying> enumShape, object? state) where TEnum : struct, Enum
        {
            return new JsonEnumConverter<TEnum>();
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
