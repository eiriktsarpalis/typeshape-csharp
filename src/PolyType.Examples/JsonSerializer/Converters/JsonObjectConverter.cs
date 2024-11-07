using System.Buffers;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using PolyType.Abstractions;

namespace PolyType.Examples.JsonSerializer.Converters;

internal class JsonObjectConverter<T>(JsonPropertyConverter<T>[] properties) : JsonConverter<T>
{
    private readonly JsonPropertyConverter<T>[] _propertiesToWrite = properties.Where(prop => prop.HasGetter).ToArray();

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
        foreach (JsonPropertyConverter<T> property in _propertiesToWrite)
        {
            writer.WritePropertyName(property.EncodedName);
            property.Write(writer, ref value, options);
        }
        writer.WriteEndObject();
    }
}

internal sealed class JsonObjectConverterWithDefaultCtor<T>(Func<T> defaultConstructor, JsonPropertyConverter<T>[] properties) : JsonObjectConverter<T>(properties)
{
    private readonly JsonPropertyDictionary<T> _propertiesToRead = properties.Where(prop => prop.HasSetter).ToJsonPropertyDictionary(isCaseSensitive: true);

    public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (default(T) is null && reader.TokenType is JsonTokenType.Null)
        {
            return default;
        }

        reader.EnsureTokenType(JsonTokenType.StartObject);
        reader.EnsureRead();

        T result = defaultConstructor();
        JsonPropertyDictionary<T> propertiesToRead = _propertiesToRead;

        while (reader.TokenType != JsonTokenType.EndObject)
        {
            Debug.Assert(reader.TokenType is JsonTokenType.PropertyName);

            JsonPropertyConverter<T>? jsonProperty = propertiesToRead.LookupProperty(ref reader);
            reader.EnsureRead();
            
            if (jsonProperty != null)
            {
                jsonProperty.Read(ref reader, ref result, options);
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

internal sealed class JsonObjectConverterWithParameterizedCtor<TDeclaringType, TArgumentState>(
    Func<TArgumentState> createArgumentState,
    Constructor<TArgumentState, TDeclaringType> createObject,
    JsonPropertyConverter<TArgumentState>[] constructorParameters,
    JsonPropertyConverter<TDeclaringType>[] properties) : JsonObjectConverter<TDeclaringType>(properties)
{
    // Use case-insensitive matching for constructor parameters but case-sensitive matching for property setters.
    private readonly JsonPropertyDictionary<TArgumentState> _constructorParameters = constructorParameters.Where(p => p.IsConstructorParameter).ToJsonPropertyDictionary(isCaseSensitive: false);
    private readonly JsonPropertyDictionary<TArgumentState> _propertySetters = constructorParameters.Where(p => !p.IsConstructorParameter).ToJsonPropertyDictionary(isCaseSensitive: true);

    public sealed override TDeclaringType? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (default(TDeclaringType) is null && reader.TokenType is JsonTokenType.Null)
        {
            return default;
        }

        reader.EnsureTokenType(JsonTokenType.StartObject);
        reader.EnsureRead();

        JsonPropertyDictionary<TArgumentState> ctorParams = _constructorParameters;
        JsonPropertyDictionary<TArgumentState> propertySetters = _propertySetters;
        TArgumentState argumentState = createArgumentState();

        while (reader.TokenType != JsonTokenType.EndObject)
        {
            Debug.Assert(reader.TokenType is JsonTokenType.PropertyName);

            JsonPropertyConverter<TArgumentState>? jsonProperty = ctorParams.LookupProperty(ref reader) ?? propertySetters.LookupProperty(ref reader);
            reader.EnsureRead();

            if (jsonProperty != null)
            {
                jsonProperty.Read(ref reader, ref argumentState, options);
            }
            else
            {
                reader.Skip();
            }

            reader.EnsureRead();
        }

        return createObject(ref argumentState);
    }
}