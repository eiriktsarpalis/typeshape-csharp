namespace TypeShape.Applications.JsonSerializer.Converters;

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

internal class JsonDictionaryConverter<TDictionary, TKey, TValue> : JsonConverter<TDictionary>
{
    private protected readonly JsonConverter<TKey> _keyConverter;
    private protected readonly JsonConverter<TValue> _valueConverter;
    private readonly Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> _getDictionary;

    public JsonDictionaryConverter(JsonConverter<TKey> keyConverter, JsonConverter<TValue> valueConverter, Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> getDictionary)
    {
        _keyConverter = keyConverter;
        _valueConverter = valueConverter;
        _getDictionary = getDictionary;
    }

    public override TDictionary? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotSupportedException($"Type {typeof(TDictionary)} does not support deserialization.");
    }

    public sealed override void Write(Utf8JsonWriter writer, TDictionary value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        JsonConverter<TKey> keyConverter = _keyConverter;
        JsonConverter<TValue> valueConverter = _valueConverter;

        writer.WriteStartObject();
        foreach (KeyValuePair<TKey, TValue> kvp in _getDictionary(value))
        {
            keyConverter.WriteAsPropertyName(writer, kvp.Key, options);
            valueConverter.Write(writer, kvp.Value, options);
        }
        writer.WriteEndObject();
    }
}

internal sealed class JsonMutableDictionaryConverter<TDictionary, TKey, TValue> : JsonDictionaryConverter<TDictionary, TKey, TValue>
{
    private readonly Func<TDictionary> _createObject;
    private readonly Setter<TDictionary, KeyValuePair<TKey, TValue>> _addDelegate;

    public JsonMutableDictionaryConverter(
        JsonConverter<TKey> keyConverter, 
        JsonConverter<TValue> valueConverter, 
        Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> getDictionary,
        Func<TDictionary> createObject, 
        Setter<TDictionary, KeyValuePair<TKey, TValue>> addDelegate)
        : base(keyConverter, valueConverter, getDictionary)
    {
        _createObject = createObject;
        _addDelegate = addDelegate;
    }

    public override TDictionary? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (default(TDictionary) is null && reader.TokenType is JsonTokenType.Null)
        {
            return default;
        }

        reader.EnsureTokenType(JsonTokenType.StartObject);

        TDictionary result = _createObject();
        reader.EnsureRead();

        JsonConverter<TKey> keyConverter = _keyConverter;
        JsonConverter<TValue> valueConverter = _valueConverter;
        Setter<TDictionary, KeyValuePair<TKey, TValue>> addDelegate = _addDelegate;

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
    private readonly Func<IEnumerable<KeyValuePair<TKey, TValue>>, TDictionary> _constructor;

    public JsonImmutableDictionaryConverter(
        JsonConverter<TKey> keyConverter,
        JsonConverter<TValue> valueConverter,
        Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> getDictionary,
        Func<IEnumerable<KeyValuePair<TKey, TValue>>, TDictionary> constructor)
        : base(keyConverter, valueConverter, getDictionary)
    {
        _constructor = constructor;
    }

    public override TDictionary? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (default(TDictionary) is null && reader.TokenType is JsonTokenType.Null)
        {
            return default;
        }

        reader.EnsureTokenType(JsonTokenType.StartObject);

        List<KeyValuePair<TKey, TValue>> buffer = new();
        reader.EnsureRead();

        JsonConverter<TKey> keyConverter = _keyConverter;
        JsonConverter<TValue> valueConverter = _valueConverter;

        while (reader.TokenType != JsonTokenType.EndObject)
        {
            Debug.Assert(reader.TokenType == JsonTokenType.PropertyName);

            TKey key = keyConverter.ReadAsPropertyName(ref reader, typeof(TKey), options);
            reader.EnsureRead();
            TValue value = valueConverter.Read(ref reader, typeof(TValue), options)!;
            reader.EnsureRead();
            buffer.Add(new(key, value));
        }

        return _constructor(buffer);
    }
}
