namespace TypeShape.Applications.JsonSerializer.Converters;

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

internal sealed class JsonDictionaryConverter<TDictionary, TKey, TValue> : JsonConverter<TDictionary>
{
    internal JsonConverter<TKey>? KeyConverter { get; set; }
    internal JsonConverter<TValue>? ValueConverter { get; set; }
    internal Func<TDictionary, IEnumerable<KeyValuePair<TKey, TValue>>>? GetEnumerable { get; set; }
    internal Func<TDictionary>? CreateObject { get; set; }
    internal Setter<TDictionary, KeyValuePair<TKey, TValue>>? AddDelegate { get; set; }

    public override TDictionary? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (default(TDictionary) is null && reader.TokenType is JsonTokenType.Null)
        {
            return default;
        }

        Func<TDictionary>? createObject = CreateObject;
        if (createObject is null)
        {
            ThrowNotSupportedException();
            [DoesNotReturn] static void ThrowNotSupportedException() => throw new NotSupportedException();
        }

        reader.EnsureTokenType(JsonTokenType.StartObject);

        TDictionary result = createObject();
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

            //TKey key = keyConverter.ReadAsPropertyName(ref reader, typeof(TKey), options);
            TKey key = (TKey)Convert.ChangeType(reader.GetString(), typeof(TKey))!;
            reader.EnsureRead();
            TValue value = valueConverter.Read(ref reader, typeof(TValue), options)!;
            reader.EnsureRead();

            addDelegate(ref result, new(key, value));
        }

        return result;
    }

    public override void Write(Utf8JsonWriter writer, TDictionary value, JsonSerializerOptions options)
    {
        Debug.Assert(GetEnumerable != null);
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
        foreach (KeyValuePair<TKey, TValue> kvp in GetEnumerable(value))
        {
            //keyConverter.WriteAsPropertyName(writer, kvp.Key, options);
            writer.WritePropertyName(kvp.Key!.ToString()!);
            valueConverter.Write(writer, kvp.Value, options);
        }
        writer.WriteEndObject();
    }
}
