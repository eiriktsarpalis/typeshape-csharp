namespace TypeShape.Applications.JsonSerializer.Converters;

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

internal sealed class JsonObjectConverter<T> : JsonConverter<T>
{
    private JsonProperty<T>[]? _writtenProperties;
    private Dictionary<string, JsonProperty<T>>? _readProperties;
    private JsonConstructor<T>? _constructor;

    public void Configure(JsonConstructor<T>? constructor, JsonProperty<T>[] properties)
    {
        _writtenProperties = properties.Where(prop => prop.CanWrite).ToArray();

        if (constructor != null)
        {
            _constructor = constructor;
            _readProperties = properties
                .Where(prop => prop.CanRead)
                .ToDictionary(prop => prop.Name);
        }
    }

    public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (default(T) is null && reader.TokenType is JsonTokenType.Null)
        {
            return default;
        }

        JsonConstructor<T>? constructor = _constructor;
        if (constructor is null)
        {
            ThrowNotSupportedException();
            [DoesNotReturn] static void ThrowNotSupportedException() => throw new NotSupportedException();
        }

        reader.EnsureTokenType(JsonTokenType.StartObject);
        reader.EnsureRead();

        T result = constructor.ReadConstructorParametersAndCreateObject(ref reader, options);

        Debug.Assert(_readProperties != null);
        Dictionary<string, JsonProperty<T>> readProperties = _readProperties;

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

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        Debug.Assert(_writtenProperties != null);

        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartObject();
        foreach (JsonProperty<T> property in _writtenProperties)
        {
            property.Serialize(writer, ref value, options);
        }
        writer.WriteEndObject();
    }
}

public sealed class JsonObjectConverter : JsonConverter<object?>
{
    public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => JsonMetadataServices.ObjectConverter.Read(ref reader, typeToConvert, options);

    public override void Write(Utf8JsonWriter writer, object? value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case null: writer.WriteNullValue(); break;
            case bool b: writer.WriteBooleanValue(b); break;
            case int i: writer.WriteNumberValue(i); break;
            case string s: writer.WriteStringValue(s); break;
            default:
                writer.WriteStartObject();
                writer.WriteEndObject();
                break;
        }
    }
}