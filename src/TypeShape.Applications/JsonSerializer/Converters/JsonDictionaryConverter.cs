namespace TypeShape.Applications.JsonSerializer.Converters;

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

internal class JsonDictionaryConverter<TDictionary, TKey, TValue> : JsonConverter<TDictionary>
{
    internal JsonConverter<TKey>? KeyConverter { get; set; }
    internal JsonConverter<TValue>? ValueConverter { get; set; }
    internal Func<TDictionary, IReadOnlyDictionary<TKey, TValue>>? GetDictionary { get; set; }

    public override TDictionary? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotSupportedException($"Type {typeof(TDictionary)} does not support deserialization.");
    }

    public sealed override void Write(Utf8JsonWriter writer, TDictionary value, JsonSerializerOptions options)
    {
        Debug.Assert(GetDictionary != null);
        Debug.Assert(KeyConverter != null);
        Debug.Assert(ValueConverter != null);

        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        JsonConverter<TKey> keyConverter = KeyConverter;
        JsonConverter<TValue> valueConverter = ValueConverter;

        writer.WriteStartObject();
        foreach (KeyValuePair<TKey, TValue> kvp in GetDictionary(value))
        {
            keyConverter.WriteAsPropertyName(writer, kvp.Key, options);
            valueConverter.Write(writer, kvp.Value, options);
        }
        writer.WriteEndObject();
    }
}

internal sealed class JsonMutableDictionaryConverter<TDictionary, TKey, TValue> : JsonDictionaryConverter<TDictionary, TKey, TValue>
{
    internal required Func<TDictionary> CreateObject { get; set; }
    internal required Setter<TDictionary, KeyValuePair<TKey, TValue>> AddDelegate { get; set; }

    public override TDictionary? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (default(TDictionary) is null && reader.TokenType is JsonTokenType.Null)
        {
            return default;
        }

        reader.EnsureTokenType(JsonTokenType.StartObject);

        TDictionary result = CreateObject();
        reader.EnsureRead();

        Debug.Assert(KeyConverter != null);
        Debug.Assert(ValueConverter != null);
        Debug.Assert(AddDelegate != null);
        JsonConverter<TKey> keyConverter = KeyConverter;
        JsonConverter<TValue> valueConverter = ValueConverter;
        Setter<TDictionary, KeyValuePair<TKey, TValue>> addDelegate = AddDelegate;

        while (reader.TokenType != JsonTokenType.EndObject)
        {
            Debug.Assert(reader.TokenType == JsonTokenType.PropertyName);

            TKey key = keyConverter.ReadAsPropertyName(ref reader, typeof(TKey), options);
            reader.EnsureRead();
            TValue value = valueConverter.Read(ref reader, typeof(TValue), options)!;
            reader.EnsureRead();

            addDelegate(ref result, new(key, value));
        }

        return result;
    }
}

internal sealed class JsonImmutableDictionaryConverter<TDictionary, TKey, TValue> : JsonDictionaryConverter<TDictionary, TKey, TValue>
{
    public required Func<IEnumerable<KeyValuePair<TKey, TValue>>, TDictionary> Constructor { get; set; }

    public override TDictionary? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        Debug.Assert(KeyConverter != null);
        Debug.Assert(ValueConverter != null);

        if (default(TDictionary) is null && reader.TokenType is JsonTokenType.Null)
        {
            return default;
        }

        reader.EnsureTokenType(JsonTokenType.StartObject);

        List<KeyValuePair<TKey, TValue>> buffer = new();
        reader.EnsureRead();

        JsonConverter<TKey> keyConverter = KeyConverter;
        JsonConverter<TValue> valueConverter = ValueConverter;

        while (reader.TokenType != JsonTokenType.EndObject)
        {
            Debug.Assert(reader.TokenType == JsonTokenType.PropertyName);

            TKey key = keyConverter.ReadAsPropertyName(ref reader, typeof(TKey), options);
            reader.EnsureRead();
            TValue value = valueConverter.Read(ref reader, typeof(TValue), options)!;
            reader.EnsureRead();
            buffer.Add(new(key, value));
        }

        return Constructor(buffer);
    }
}
