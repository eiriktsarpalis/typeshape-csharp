namespace TypeShape.Applications.JsonSerializer;

using System.Diagnostics;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using TypeShape.Applications.JsonSerializer.Converters;

public partial class TypeShapeJsonResolver
{
    private sealed class ConverterBuilder : ITypeShapeVisitor
    {
        private static readonly Dictionary<Type, JsonConverter> s_defaultConverters = new()
        {
            [typeof(bool)] = JsonMetadataServices.BooleanConverter,
            [typeof(string)] = JsonMetadataServices.StringConverter,
            [typeof(sbyte)] = JsonMetadataServices.SByteConverter,
            [typeof(short)] = JsonMetadataServices.Int16Converter,
            [typeof(int)] = JsonMetadataServices.Int32Converter,
            [typeof(long)] = JsonMetadataServices.Int64Converter,
            [typeof(byte)] = JsonMetadataServices.ByteConverter,
            [typeof(ushort)] = JsonMetadataServices.UInt16Converter,
            [typeof(uint)] = JsonMetadataServices.UInt32Converter,
            [typeof(ulong)] = JsonMetadataServices.UInt64Converter,
            [typeof(char)] = JsonMetadataServices.CharConverter,
            [typeof(string)] = JsonMetadataServices.StringConverter,
            [typeof(float)] = JsonMetadataServices.SingleConverter,
            [typeof(double)] = JsonMetadataServices.DoubleConverter,
            [typeof(decimal)] = JsonMetadataServices.DecimalConverter,
            [typeof(DateTime)] = JsonMetadataServices.DateTimeConverter,
            [typeof(TimeSpan)] = JsonMetadataServices.TimeSpanConverter,
            [typeof(object)] = new JsonObjectConverter(),
        };

        private readonly Dictionary<Type, JsonConverter> _cache = new();

        public object? VisitType<T>(IType<T> type, object? state)
        {
            if (s_defaultConverters.TryGetValue(type.Type, out JsonConverter? result) ||
                _cache.TryGetValue(type.Type, out result))
            {
                return result;
            }

            switch (type.Kind)
            {
                case TypeKind.Enum:
                    IEnumType enumType = type.GetEnumType();
                    return enumType.Accept(this, null);

                case TypeKind.Nullable:
                    INullableType nullableType = type.GetNullableType();
                    return nullableType.Accept(this, null);

                case TypeKind kind when ((kind & TypeKind.Dictionary) != 0):
                    IDictionaryType dictionaryType = type.GetDictionaryType();
                    return dictionaryType.Accept(this, null);

                case TypeKind.Enumerable:
                    IEnumerableType enumerableType = type.GetEnumerableType();
                    return enumerableType.Accept(this, null);

                default:
                    Debug.Assert(type.Kind == TypeKind.None);
                    JsonObjectConverter<T> objectConverter = new();
                    _cache.Add(type.Type, objectConverter);

                    IConstructor? ctor = type
                        .GetConstructors(nonPublic: false)
                        .OrderByDescending(ctor => ctor.ParameterCount)
                        .FirstOrDefault();

                    JsonConstructor<T>? createObject = (JsonConstructor<T>?)ctor?.Accept(this, state);
                    JsonProperty<T>[] properties = type
                        .GetProperties(nonPublic: false, includeFields: true)
                        .Select(prop => (JsonProperty<T>)prop.Accept(this, state)!)
                        .ToArray();

                    objectConverter.Configure(createObject, properties);
                    return objectConverter;
            }
        }

        public object? VisitProperty<TDeclaringType, TPropertyType>(IProperty<TDeclaringType, TPropertyType> property, object? state)
        {
            JsonConverter<TPropertyType> propertyConverter = (JsonConverter<TPropertyType>)property.PropertyType.Accept(this, null)!;
            return new JsonProperty<TDeclaringType, TPropertyType>(property, propertyConverter);
        }

        public object? VisitConstructor<TDeclaringType, TArgumentState>(IConstructor<TDeclaringType, TArgumentState> constructor, object? state)
        {
            JsonProperty<TArgumentState>[] paramInfo = constructor
                .GetParameters()
                .Select(param => (JsonProperty<TArgumentState>)param.Accept(this, null)!)
                .ToArray();

            return paramInfo.Length == 0 
                ? new JsonDefaultConstructor<TDeclaringType>(constructor.GetDefaultConstructor())
                : new JsonParameterizedConstructor<TDeclaringType, TArgumentState>(constructor, paramInfo);
        }

        public object? VisitConstructorParameter<TArgumentState, TParameter>(IConstructorParameter<TArgumentState, TParameter> parameter, object? state)
        {
            JsonConverter<TParameter> paramConverter = (JsonConverter<TParameter>)parameter.ParameterType.Accept(this, null)!;
            return new JsonProperty<TArgumentState, TParameter>(parameter, paramConverter);
        }

        public object? VisitEnumerableType<TEnumerable, TElement>(IEnumerableType<TEnumerable, TElement> enumerableType, object? state)
        {
            if (typeof(TEnumerable) == typeof(TElement[]))
            {
                JsonArrayConverter<TElement> arrayConverter = new();
                _cache.Add(enumerableType.EnumerableType.Type, arrayConverter);
                arrayConverter.ElementConverter = (JsonConverter<TElement>)enumerableType.ElementType.Accept(this, null)!;
                return arrayConverter;
            }

            JsonEnumerableConverter<TEnumerable, TElement> collectionConverter = new();
            _cache.Add(enumerableType.EnumerableType.Type, collectionConverter);

            collectionConverter.ElementConverter = (JsonConverter<TElement>)enumerableType.ElementType.Accept(this, null)!;
            collectionConverter.GetEnumerable = enumerableType.GetGetEnumerable();

            if (enumerableType.IsMutable)
            {
                IConstructor? defaultCtor = enumerableType.EnumerableType
                    .GetConstructors(nonPublic: false)
                    .FirstOrDefault(ctor => ctor.ParameterCount == 0);

                if (defaultCtor != null)
                {
                    JsonConstructor<TEnumerable> constructor = (JsonConstructor<TEnumerable>)defaultCtor.Accept(this, null)!;
                    collectionConverter.CreateObject = constructor.DefaultConstructor;
                    collectionConverter.AddDelegate = enumerableType.GetAddDelegate();
                }
            }

            return collectionConverter;
        }

        public object? VisitDictionaryType<TDictionary, TKey, TValue>(IDictionaryType<TDictionary, TKey, TValue> dictionaryType, object? state)
            where TKey : notnull
        {
            JsonDictionaryConverter<TDictionary, TKey, TValue> dictionaryConverter = new();
            _cache.Add(dictionaryType.DictionaryType.Type, dictionaryConverter);

            dictionaryConverter.KeyConverter = (JsonConverter<TKey>)dictionaryType.KeyType.Accept(this, null)!;
            dictionaryConverter.ValueConverter = (JsonConverter<TValue>)dictionaryType.ValueType.Accept(this, null)!;
            dictionaryConverter.GetEnumerable = dictionaryType.GetGetEnumerable();

            if (dictionaryType.IsMutable)
            {

                IConstructor? defaultCtor = dictionaryType.DictionaryType
                    .GetConstructors(nonPublic: false)
                    .FirstOrDefault(ctor => ctor.ParameterCount == 0);

                if (defaultCtor != null)
                {
                    JsonConstructor<TDictionary> constructor = (JsonConstructor<TDictionary>)defaultCtor.Accept(this, null)!;
                    dictionaryConverter.CreateObject = constructor.DefaultConstructor;
                    dictionaryConverter.AddDelegate = dictionaryType.GetAddDelegate();
                }
            }

            return dictionaryConverter;
        }

        public object? VisitNullable<T>(INullableType<T> nullableType, object? state) where T : struct
        {
            JsonNullableConverter<T> converter = new();
            _cache.Add(nullableType.NullableType.Type, converter);
            converter.ElementConverter = (JsonConverter<T>)nullableType.ElementType.Accept(this, null)!;
            return converter;
        }

        public object? VisitEnum<TEnum, TUnderlying>(IEnumType<TEnum, TUnderlying> enumType, object? state) where TEnum : struct, Enum
            => new JsonEnumConverter<TEnum>();
    }
}
