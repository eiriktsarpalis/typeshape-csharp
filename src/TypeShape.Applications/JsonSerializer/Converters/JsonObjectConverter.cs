namespace TypeShape.Applications.JsonSerializer.Converters;

using System.Buffers;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

internal class JsonObjectConverter<T> : JsonConverter<T>
{
    private JsonProperty<T>[] _propertiesToWrite;

    public JsonObjectConverter(JsonProperty<T>[] properties)
    {
        _propertiesToWrite = properties.Where(prop => prop.HasGetter).ToArray();
    }

    public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => throw new NotSupportedException($"Deserialization for type {typeof(T)} is not supported.");

    public sealed override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        Debug.Assert(_propertiesToWrite != null);

        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartObject();
        foreach (JsonProperty<T> property in _propertiesToWrite)
        {
            property.Serialize(writer, ref value, options);
        }
        writer.WriteEndObject();
    }
}

internal abstract class JsonObjectConverterWithCtor<T> : JsonObjectConverter<T>
{
    private readonly JsonPropertyDictionary<T> _propertiesToRead;

    public abstract T ReadConstructorParametersAndCreateObject(scoped ref Utf8JsonReader reader, JsonSerializerOptions options);

    public JsonObjectConverterWithCtor(JsonProperty<T>[] properties)
        : base(properties)
    {
        _propertiesToRead = JsonPropertyDictionary.Create(properties.Where(prop => prop.HasSetter), isCaseSensitive: true);
    }

    public sealed override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (default(T) is null && reader.TokenType is JsonTokenType.Null)
        {
            return default;
        }

        reader.EnsureTokenType(JsonTokenType.StartObject);
        reader.EnsureRead();

        T result = ReadConstructorParametersAndCreateObject(ref reader, options);
        JsonPropertyDictionary<T> propertiesToRead = _propertiesToRead;

        while (reader.TokenType != JsonTokenType.EndObject)
        {
            Debug.Assert(reader.TokenType == JsonTokenType.PropertyName);

            JsonProperty<T>? jsonProperty = propertiesToRead.LookupProperty(ref reader);
            reader.EnsureRead();
            
            if (jsonProperty != null)
            {
                jsonProperty.Deserialize(ref reader, ref result, options);
            }
            else
            {
                reader.Skip();
            }

            reader.EnsureRead();
        }

        return result;
    }
}

internal sealed class JsonObjectConverterWithDefaultCtor<T> : JsonObjectConverterWithCtor<T>
{
    private readonly Func<T> _defaultConstructor;

    public JsonObjectConverterWithDefaultCtor(Func<T> defaultConstructor, JsonProperty<T>[] properties)
        : base(properties)
    {
        _defaultConstructor = defaultConstructor;
    }

    public override T ReadConstructorParametersAndCreateObject(scoped ref Utf8JsonReader reader, JsonSerializerOptions options)
        => _defaultConstructor();
}

internal sealed class JsonObjectConverterWithParameterizedCtor<TDeclaringType, TArgumentState> : JsonObjectConverterWithCtor<TDeclaringType>
{
    private readonly Func<TArgumentState> _createArgumentState;
    private readonly Func<TArgumentState, TDeclaringType> _createObject;
    private readonly JsonPropertyDictionary<TArgumentState> _constructorParameters;

    public JsonObjectConverterWithParameterizedCtor(
        Func<TArgumentState> createArgumentState, 
        Func<TArgumentState, TDeclaringType> createObject,
        JsonProperty<TArgumentState>[] constructorParameters,
        JsonProperty<TDeclaringType>[] properties)
        : base(properties)
    {
        _createArgumentState = createArgumentState;
        _createObject = createObject;
        _constructorParameters = JsonPropertyDictionary.Create(constructorParameters, isCaseSensitive: false);
    }

    public override TDeclaringType ReadConstructorParametersAndCreateObject(scoped ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        JsonPropertyDictionary<TArgumentState> ctorParams = _constructorParameters;
        TArgumentState argumentState = _createArgumentState();

        while (reader.TokenType != JsonTokenType.EndObject)
        {
            Debug.Assert(reader.TokenType is JsonTokenType.PropertyName);

            JsonProperty<TArgumentState>? jsonProperty = ctorParams.LookupProperty(ref reader);
            if (jsonProperty is null)
            {
                // stop reading constructor arguments on the first unrecognized parameter
                break;
            }

            reader.EnsureRead();
            jsonProperty.Deserialize(ref reader, ref argumentState, options);
            reader.EnsureRead();
        }

        return _createObject(argumentState);
    }
}