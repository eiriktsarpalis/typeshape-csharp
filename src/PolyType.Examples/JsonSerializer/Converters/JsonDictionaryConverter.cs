using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using PolyType.Abstractions;
using PolyType.Examples.Utilities;

namespace PolyType.Examples.JsonSerializer.Converters;

internal class JsonDictionaryConverter<TDictionary, TKey, TValue>(JsonConverter<TKey> keyConverter, JsonConverter<TValue> valueConverter, IDictionaryShape<TDictionary, TKey, TValue> shape) : JsonConverter<TDictionary>
    where TKey : notnull
{
    private static readonly bool s_isDictionary = typeof(Dictionary<TKey, TValue>).IsAssignableFrom(typeof(TDictionary));
    private protected readonly JsonConverter<TKey> _keyConverter = keyConverter;
    private protected readonly JsonConverter<TValue> _valueConverter = valueConverter;
    private readonly Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> _getDictionary = shape.GetGetDictionary();

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
        
        writer.WriteStartObject();
        
        if (s_isDictionary)
        {
            WriteEntriesAsDictionary(writer, (Dictionary<TKey, TValue>)(object)value, options);
        }
        else
        {
            WriteEntriesAsReadOnlyDictionary(writer, value, options);
        }

        writer.WriteEndObject();
    }

    private void WriteEntriesAsDictionary(Utf8JsonWriter writer, Dictionary<TKey, TValue> value, JsonSerializerOptions options)
    {
        JsonConverter<TKey> keyConverter = _keyConverter;
        JsonConverter<TValue> valueConverter = _valueConverter;

        foreach (KeyValuePair<TKey, TValue> entry in value)
        {
            keyConverter.WriteAsPropertyName(writer, entry.Key, options);
            valueConverter.Write(writer, entry.Value, options);
        }
    }

    private void WriteEntriesAsReadOnlyDictionary(Utf8JsonWriter writer, TDictionary value, JsonSerializerOptions options)
    {
        JsonConverter<TKey> keyConverter = _keyConverter;
        JsonConverter<TValue> valueConverter = _valueConverter;

        foreach (KeyValuePair<TKey, TValue> entry in _getDictionary(value))
        {
            keyConverter.WriteAsPropertyName(writer, entry.Key, options);
            valueConverter.Write(writer, entry.Value, options);
        }
    }
}

internal sealed class JsonMutableDictionaryConverter<TDictionary, TKey, TValue>(
    JsonConverter<TKey> keyConverter,
    JsonConverter<TValue> valueConverter,
    IDictionaryShape<TDictionary, TKey, TValue> shape,
    Func<TDictionary> createObject,
    Setter<TDictionary, KeyValuePair<TKey, TValue>> addDelegate) : JsonDictionaryConverter<TDictionary, TKey, TValue>(keyConverter, valueConverter, shape)
    where TKey : notnull
{
    private readonly Setter<TDictionary, KeyValuePair<TKey, TValue>> _addDelegate = addDelegate;

    public override TDictionary? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (default(TDictionary) is null && reader.TokenType is JsonTokenType.Null)
        {
            return default;
        }

        reader.EnsureTokenType(JsonTokenType.StartObject);

        TDictionary result = createObject();
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

internal abstract class JsonImmutableDictionaryConverter<TDictionary, TKey, TValue>(
    JsonConverter<TKey> keyConverter,
    JsonConverter<TValue> valueConverter,
    IDictionaryShape<TDictionary, TKey, TValue> shape)
    : JsonDictionaryConverter<TDictionary, TKey, TValue>(keyConverter, valueConverter, shape)
    where TKey : notnull
{
    private protected abstract TDictionary Construct(PooledList<KeyValuePair<TKey, TValue>> buffer);

    public sealed override TDictionary? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (default(TDictionary) is null && reader.TokenType is JsonTokenType.Null)
        {
            return default;
        }

        reader.EnsureTokenType(JsonTokenType.StartObject);

        using PooledList<KeyValuePair<TKey, TValue>> buffer = new();
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

        return Construct(buffer);
    }
}

internal sealed class JsonEnumerableConstructorDictionaryConverter<TDictionary, TKey, TValue>(
    JsonConverter<TKey> keyConverter,
    JsonConverter<TValue> valueConverter,
    IDictionaryShape<TDictionary, TKey, TValue> shape,
    Func<IEnumerable<KeyValuePair<TKey, TValue>>, TDictionary> constructor)
    : JsonImmutableDictionaryConverter<TDictionary, TKey, TValue>(keyConverter, valueConverter, shape)
    where TKey : notnull
{
    private protected override TDictionary Construct(PooledList<KeyValuePair<TKey, TValue>> buffer)
        => constructor(buffer.ExchangeToArraySegment());
}

internal sealed class JsonSpanConstructorDictionaryConverter<TDictionary, TKey, TValue>(
    JsonConverter<TKey> keyConverter,
    JsonConverter<TValue> valueConverter,
    IDictionaryShape<TDictionary, TKey, TValue> shape,
    SpanConstructor<KeyValuePair<TKey, TValue>, TDictionary> constructor)
    : JsonImmutableDictionaryConverter<TDictionary, TKey, TValue>(keyConverter, valueConverter, shape)
    where TKey : notnull
{
    private protected override TDictionary Construct(PooledList<KeyValuePair<TKey, TValue>> buffer)
        => constructor(buffer.AsSpan());
}
