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
            if (state is "getCtorFunc")
            {
                Debug.Assert(constructor.ParameterCount is 0 or 1);
                if (constructor.ParameterCount == 0) 
                { 
                    return constructor.GetDefaultConstructor();
                }
                else
                {
                    Debug.Assert(typeof(TArgumentState) == constructor.GetParameters().First().ParameterType.Type);
                    return constructor.GetParameterizedConstructor();
                }
            }

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
            JsonEnumerableConverter<TEnumerable, TElement> enumerableConverter;

            if (GetDefaultConstructorShape(enumerableShape) is { } defaultCtor)
            {
                enumerableConverter = new JsonMutableEnumerableConverter<TEnumerable, TElement>
                {
                    CreateObject = (Func<TEnumerable>)defaultCtor.Accept(this, state: "getCtorFunc")!,
                    AddDelegate = enumerableShape.GetAddElement(),
                };
            }
            else if (GetEnumerableConstructorShape(enumerableShape) is { } enumerableCtor)
            {
                var ctor = (Func<IEnumerable<TElement>, TEnumerable>)enumerableCtor.Accept(this, "getCtorFunc")!;
                enumerableConverter = new JsonImmutableEnumerableConverter<TEnumerable, TElement>
                {
                    Constructor = ctor
                };
            }
            else
            {
                enumerableConverter = new JsonEnumerableConverter<TEnumerable, TElement>();
            }

            _visited.Add(enumerableShape.Type.Type, enumerableConverter);
            enumerableConverter.ElementConverter = (JsonConverter<TElement>)enumerableShape.ElementType.Accept(this, null)!;
            enumerableConverter.GetEnumerable = enumerableShape.GetGetEnumerable();
            return enumerableConverter;

            static IConstructorShape? GetDefaultConstructorShape(IEnumerableShape shape)
            {
                if (shape.IsMutable)
                {
                    return shape.Type
                        .GetConstructors(nonPublic: false)
                        .FirstOrDefault(ctor => ctor.ParameterCount == 0);
                }

                return null;
            }

            static IConstructorShape? GetEnumerableConstructorShape(IEnumerableShape shape)
            {
                return shape.Type
                    .GetConstructors(nonPublic: false)
                    .Where(ctor => ctor.ParameterCount == 1)
                    .FirstOrDefault(ctor => ctor.GetParameters().First().ParameterType.Type == typeof(IEnumerable<TElement>));
            }
        }

        public object? VisitDictionary<TDictionary, TKey, TValue>(IDictionaryShape<TDictionary, TKey, TValue> dictionaryShape, object? state)
            where TKey : notnull
        {
            JsonDictionaryConverter<TDictionary, TKey, TValue> dictionaryConverter;

            if (GetDefaultConstructorShape(dictionaryShape) is IConstructorShape defaultCtorShape)
            {
                dictionaryConverter = new JsonMutableDictionaryConverter<TDictionary, TKey, TValue>
                {
                    CreateObject = (Func<TDictionary>)defaultCtorShape.Accept(this, state: "getCtorFunc")!,
                    AddDelegate = dictionaryShape.GetAddKeyValuePair(),
                };
            }
            else if (GetEnumerableConstructorShape(dictionaryShape) is IConstructorShape enumerableCtorShape)
            {
                dictionaryConverter = new JsonImmutableDictionaryConverter<TDictionary, TKey, TValue>
                {
                    Constructor = (Func<IEnumerable<KeyValuePair<TKey, TValue>>, TDictionary>)enumerableCtorShape.Accept(this, "getCtorFunc")!,
                };
            }
            else
            {
                dictionaryConverter = new();
            }

            _visited.Add(dictionaryShape.Type.Type, dictionaryConverter);

            dictionaryConverter.KeyConverter = (JsonConverter<TKey>)dictionaryShape.KeyType.Accept(this, null)!;
            dictionaryConverter.ValueConverter = (JsonConverter<TValue>)dictionaryShape.ValueType.Accept(this, null)!;
            dictionaryConverter.GetDictionary = dictionaryShape.GetGetDictionary();

            return dictionaryConverter;

            static IConstructorShape? GetDefaultConstructorShape(IDictionaryShape shape)
            {
                if (shape.IsMutable)
                {
                    return shape.Type
                        .GetConstructors(nonPublic: false)
                        .FirstOrDefault(ctor => ctor.ParameterCount == 0);
                }

                return null;
            }

            static IConstructorShape? GetEnumerableConstructorShape(IDictionaryShape shape)
            {
                return shape.Type
                    .GetConstructors(nonPublic: false)
                    .Where(ctor => ctor.ParameterCount == 1)
                    .FirstOrDefault(ctor => ctor.GetParameters().First().ParameterType.Type == typeof(IEnumerable<KeyValuePair<TKey, TValue>>));
            }
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
