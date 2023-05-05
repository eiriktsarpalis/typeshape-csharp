namespace TypeShape.Applications.JsonSerializer.Converters;

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
    private Dictionary<string, JsonProperty<T>>? _propertiesToRead;

    public abstract T ReadConstructorParametersAndCreateObject(ref Utf8JsonReader reader, JsonSerializerOptions options);

    public JsonObjectConverterWithCtor(JsonProperty<T>[] properties)
        : base(properties)
    {
        _propertiesToRead = properties
            .Where(prop => prop.HasSetter)
            .ToDictionary(prop => prop.Name);
    }

    public sealed override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        Debug.Assert(_propertiesToRead != null);

        if (default(T) is null && reader.TokenType is JsonTokenType.Null)
        {
            return default;
        }

        reader.EnsureTokenType(JsonTokenType.StartObject);
        reader.EnsureRead();

        T result = ReadConstructorParametersAndCreateObject(ref reader, options);
        Dictionary<string, JsonProperty<T>> readProperties = _propertiesToRead;

        while (reader.TokenType != JsonTokenType.EndObject)
        {
            Debug.Assert(reader.TokenType == JsonTokenType.PropertyName);
            string propertyName = reader.GetString()!;
            reader.EnsureRead();

            if (readProperties.TryGetValue(propertyName, out JsonProperty<T>? jsonProperty))
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

    public override T ReadConstructorParametersAndCreateObject(ref Utf8JsonReader reader, JsonSerializerOptions options)
        => _defaultConstructor();
}

internal sealed class JsonObjectConverterWithParameterizedCtor<TDeclaringType, TArgumentState> : JsonObjectConverterWithCtor<TDeclaringType>
{
    private readonly Func<TArgumentState> _createArgumentState;
    private readonly Func<TArgumentState, TDeclaringType> _createObject;
    private readonly Dictionary<string, JsonProperty<TArgumentState>> _constructorParameters;

    public JsonObjectConverterWithParameterizedCtor(
        Func<TArgumentState> createArgumentState, 
        Func<TArgumentState, TDeclaringType> createObject,
        JsonProperty<TArgumentState>[] constructorParameters,
        JsonProperty<TDeclaringType>[] properties)
        : base(properties)
    {
        _createArgumentState = createArgumentState;
        _createObject = createObject;
        _constructorParameters = constructorParameters.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
    }

    public override TDeclaringType ReadConstructorParametersAndCreateObject(ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        Dictionary<string, JsonProperty<TArgumentState>> ctorParams = _constructorParameters;
        TArgumentState argumentState = _createArgumentState();

        while (reader.TokenType != JsonTokenType.EndObject)
        {
            Debug.Assert(reader.TokenType is JsonTokenType.PropertyName);
            string propertyName = reader.GetString()!;

            if (!ctorParams.TryGetValue(propertyName, out JsonProperty<TArgumentState>? jsonProperty))
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
