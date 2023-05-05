namespace TypeShape.Applications.JsonSerializer;

using System.Diagnostics;
using System.Numerics;
using System.Text;
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
            [typeof(Int128)] = new Int128Converter(),
            [typeof(byte)] = JsonMetadataServices.ByteConverter,
            [typeof(ushort)] = JsonMetadataServices.UInt16Converter,
            [typeof(uint)] = JsonMetadataServices.UInt32Converter,
            [typeof(ulong)] = JsonMetadataServices.UInt64Converter,
            [typeof(UInt128)] = new UInt128Converter(),
            [typeof(char)] = JsonMetadataServices.CharConverter,
            [typeof(string)] = JsonMetadataServices.StringConverter,
            [typeof(Half)] = new HalfConverter(),
            [typeof(float)] = JsonMetadataServices.SingleConverter,
            [typeof(double)] = JsonMetadataServices.DoubleConverter,
            [typeof(decimal)] = JsonMetadataServices.DecimalConverter,
            [typeof(DateTime)] = JsonMetadataServices.DateTimeConverter,
            [typeof(TimeSpan)] = JsonMetadataServices.TimeSpanConverter,
            [typeof(DateOnly)] = JsonMetadataServices.DateOnlyConverter,
            [typeof(TimeOnly)] = JsonMetadataServices.TimeOnlyConverter,
            [typeof(Guid)] = JsonMetadataServices.GuidConverter,
            [typeof(BigInteger)] = new BigIntegerConverter(),
            [typeof(Rune)] = new RuneConverter(),
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

                    IConstructorShape? ctor = type
                        .GetConstructors(nonPublic: false)
                        .OrderByDescending(ctor => ctor.ParameterCount)
                        .FirstOrDefault();

                    JsonObjectConverter<T> objectConverter = ctor != null
                        ? (JsonObjectConverter<T>)ctor.Accept(this, null)!
                        : new JsonObjectConverter<T>();

                    _visited.Add(typeof(T), objectConverter);

                    JsonProperty<T>[] properties = type
                        .GetProperties(nonPublic: false, includeFields: true)
                        .Select(prop => (JsonProperty<T>)prop.Accept(this, state)!)
                        .ToArray();

                    objectConverter.Configure(properties);
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
            if (constructor.ParameterCount == 0)
            {
                return new JsonObjectConverterWithDefaultCtor<TDeclaringType>(constructor.GetDefaultConstructor());
            }

            // Delay constructor param resolution to avoid stack overflows in recursive types
            Lazy<JsonProperty<TArgumentState>[]> constructorParams = new(() => constructor
                .GetParameters()
                .Select(param => (JsonProperty<TArgumentState>)param.Accept(this, null)!)
                .ToArray());

            return new JsonObjectConverterWithParameterizedCtor<TDeclaringType, TArgumentState>(
                constructor.GetArgumentStateConstructor(), 
                constructor.GetParameterizedConstructor(), 
                constructorParams);
        }

        public object? VisitConstructorParameter<TArgumentState, TParameter>(IConstructorParameterShape<TArgumentState, TParameter> parameter, object? state)
        {
            JsonConverter<TParameter> paramConverter = (JsonConverter<TParameter>)parameter.ParameterType.Accept(this, null)!;
            return new JsonProperty<TArgumentState, TParameter>(parameter, paramConverter);
        }

        public object? VisitEnumerable<TEnumerable, TElement>(IEnumerableShape<TEnumerable, TElement> enumerableShape, object? state)
        {
            JsonEnumerableConverter<TEnumerable, TElement> enumerableConverter;
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
                    enumerableConverter = new JsonMutableEnumerableConverter<TEnumerable, TElement>
                    {
                        CreateObject = constructor.GetDefaultConstructor(),
                        AddDelegate = enumerableShape.GetAddElement(),
                    };
                    break;

                case IConstructorShape<TEnumerable, IEnumerable<TElement>> enumerableCtor:
                    Debug.Assert(constructor.ParameterCount == 1);
                    enumerableConverter = new JsonImmutableEnumerableConverter<TEnumerable, TElement>()
                    {
                        Constructor = enumerableCtor.GetParameterizedConstructor()
                    };
                    break;

                default:
                    Debug.Assert(constructor is null);
                    enumerableConverter = new();
                    break;
            }

            _visited.Add(typeof(TEnumerable), enumerableConverter);
            enumerableConverter.ElementConverter = (JsonConverter<TElement>)enumerableShape.ElementType.Accept(this, null)!;
            enumerableConverter.GetEnumerable = enumerableShape.GetGetEnumerable();
            return enumerableConverter;
        }

        public object? VisitDictionary<TDictionary, TKey, TValue>(IDictionaryShape<TDictionary, TKey, TValue> dictionaryShape, object? state)
            where TKey : notnull
        {
            JsonDictionaryConverter<TDictionary, TKey, TValue> dictionaryConverter;
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
                    dictionaryConverter = new JsonMutableDictionaryConverter<TDictionary, TKey, TValue>
                    {
                        CreateObject = constructor.GetDefaultConstructor(),
                        AddDelegate = dictionaryShape.GetAddKeyValuePair(),
                    };
                    break;

                case IConstructorShape<TDictionary, IEnumerable<KeyValuePair<TKey, TValue>>> enumerableCtor:
                    Debug.Assert(constructor.ParameterCount == 1);
                    dictionaryConverter = new JsonImmutableDictionaryConverter<TDictionary, TKey, TValue>
                    {
                        Constructor = enumerableCtor.GetParameterizedConstructor(),
                    };
                    break;

                default:
                    Debug.Assert(constructor is null);
                    dictionaryConverter = new();
                    break;
            }

            _visited.Add(typeof(TDictionary), dictionaryConverter);

            dictionaryConverter.KeyConverter = (JsonConverter<TKey>)dictionaryShape.KeyType.Accept(this, null)!;
            dictionaryConverter.ValueConverter = (JsonConverter<TValue>)dictionaryShape.ValueType.Accept(this, null)!;
            dictionaryConverter.GetDictionary = dictionaryShape.GetGetDictionary();

            return dictionaryConverter;
        }

        public object? VisitNullable<T>(INullableShape<T> nullableShape, object? state) where T : struct
        {
            JsonNullableConverter<T> converter = new();
            _visited.Add(typeof(T?), converter);
            converter.ElementConverter = (JsonConverter<T>)nullableShape.ElementType.Accept(this, null)!;
            return converter;
        }

        public object? VisitEnum<TEnum, TUnderlying>(IEnumShape<TEnum, TUnderlying> enumShape, object? state) where TEnum : struct, Enum
            => new JsonEnumConverter<TEnum>();
    }
}
