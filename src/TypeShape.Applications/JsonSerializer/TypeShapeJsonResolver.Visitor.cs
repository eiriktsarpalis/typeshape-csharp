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

        private readonly Dictionary<Type, JsonConverter> _visited = new();

        public object? VisitType<T>(ITypeShape<T> type, object? state)
        {
            if (s_defaultConverters.TryGetValue(type.Type, out JsonConverter? result) ||
                _visited.TryGetValue(type.Type, out result))
            {
                return result;
            }

            switch (type.Kind)
            {
                case TypeKind.Enum:
                    IEnumShape enumShape = type.GetEnumShape();
                    return enumShape.Accept(this, null);

                case TypeKind.Nullable:
                    INullableShape nullableShape = type.GetNullableShape();
                    return nullableShape.Accept(this, null);

                case TypeKind kind when ((kind & TypeKind.Dictionary) != 0):
                    IDictionaryShape dictionaryShape = type.GetDictionaryShape();
                    return dictionaryShape.Accept(this, null);

                case TypeKind.Enumerable:
                    IEnumerableShape enumerableShape = type.GetEnumerableShape();
                    return enumerableShape.Accept(this, null);

                default:
                    Debug.Assert(type.Kind == TypeKind.None);
                    JsonObjectConverter<T> objectConverter = new();
                    _visited.Add(type.Type, objectConverter);

                    IConstructorShape? ctor = type
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

        public object? VisitProperty<TDeclaringType, TPropertyType>(IPropertyShape<TDeclaringType, TPropertyType> property, object? state)
        {
            JsonConverter<TPropertyType> propertyConverter = (JsonConverter<TPropertyType>)property.PropertyType.Accept(this, null)!;
            return new JsonProperty<TDeclaringType, TPropertyType>(property, propertyConverter);
        }

        public object? VisitConstructor<TDeclaringType, TArgumentState>(IConstructorShape<TDeclaringType, TArgumentState> constructor, object? state)
        {
            JsonProperty<TArgumentState>[] paramInfo = constructor
                .GetParameters()
                .Select(param => (JsonProperty<TArgumentState>)param.Accept(this, null)!)
                .ToArray();

            return paramInfo.Length == 0 
                ? new JsonDefaultConstructor<TDeclaringType>(constructor.GetDefaultConstructor())
                : new JsonParameterizedConstructor<TDeclaringType, TArgumentState>(constructor, paramInfo);
        }

        public object? VisitConstructorParameter<TArgumentState, TParameter>(IConstructorParameterShape<TArgumentState, TParameter> parameter, object? state)
        {
            JsonConverter<TParameter> paramConverter = (JsonConverter<TParameter>)parameter.ParameterType.Accept(this, null)!;
            return new JsonProperty<TArgumentState, TParameter>(parameter, paramConverter);
        }

        public object? VisitEnumerable<TEnumerable, TElement>(IEnumerableShape<TEnumerable, TElement> enumerableShape, object? state)
        {
            if (typeof(TEnumerable) == typeof(TElement[]))
            {
                JsonArrayConverter<TElement> arrayConverter = new();
                _visited.Add(enumerableShape.Type.Type, arrayConverter);
                arrayConverter.ElementConverter = (JsonConverter<TElement>)enumerableShape.ElementType.Accept(this, null)!;
                return arrayConverter;
            }

            JsonEnumerableConverter<TEnumerable, TElement> collectionConverter = new();
            _visited.Add(enumerableShape.Type.Type, collectionConverter);

            collectionConverter.ElementConverter = (JsonConverter<TElement>)enumerableShape.ElementType.Accept(this, null)!;
            collectionConverter.GetEnumerable = enumerableShape.GetGetEnumerable();

            if (enumerableShape.IsMutable)
            {
                IConstructorShape? defaultCtor = enumerableShape.Type
                    .GetConstructors(nonPublic: false)
                    .FirstOrDefault(ctor => ctor.ParameterCount == 0);

                if (defaultCtor != null)
                {
                    JsonConstructor<TEnumerable> constructor = (JsonConstructor<TEnumerable>)defaultCtor.Accept(this, null)!;
                    collectionConverter.CreateObject = constructor.DefaultConstructor;
                    collectionConverter.AddDelegate = enumerableShape.GetAddElement();
                }
            }

            return collectionConverter;
        }

        public object? VisitDictionary<TDictionary, TKey, TValue>(IDictionaryShape<TDictionary, TKey, TValue> dictionaryShape, object? state)
            where TKey : notnull
        {
            JsonDictionaryConverter<TDictionary, TKey, TValue> dictionaryConverter = new();
            _visited.Add(dictionaryShape.Type.Type, dictionaryConverter);

            dictionaryConverter.KeyConverter = (JsonConverter<TKey>)dictionaryShape.KeyType.Accept(this, null)!;
            dictionaryConverter.ValueConverter = (JsonConverter<TValue>)dictionaryShape.ValueType.Accept(this, null)!;
            dictionaryConverter.GetDictionary = dictionaryShape.GetGetDictionary();

            if (dictionaryShape.IsMutable)
            {

                IConstructorShape? defaultCtor = dictionaryShape.Type
                    .GetConstructors(nonPublic: false)
                    .FirstOrDefault(ctor => ctor.ParameterCount == 0);

                if (defaultCtor != null)
                {
                    var constructor = (JsonConstructor<TDictionary>)defaultCtor.Accept(this, null)!;
                    dictionaryConverter.CreateObject = constructor.DefaultConstructor;
                    dictionaryConverter.AddDelegate = dictionaryShape.GetAddKeyValuePair();
                }
            }

            return dictionaryConverter;
        }

        public object? VisitNullable<T>(INullableShape<T> nullableShape, object? state) where T : struct
        {
            JsonNullableConverter<T> converter = new();
            _visited.Add(nullableShape.Type.Type, converter);
            converter.ElementConverter = (JsonConverter<T>)nullableShape.ElementType.Accept(this, null)!;
            return converter;
        }

        public object? VisitEnum<TEnum, TUnderlying>(IEnumShape<TEnum, TUnderlying> enumShape, object? state) where TEnum : struct, Enum
            => new JsonEnumConverter<TEnum>();
    }
}
