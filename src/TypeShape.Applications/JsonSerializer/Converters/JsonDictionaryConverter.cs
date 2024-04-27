namespace TypeShape.Applications.JsonSerializer.Converters;

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeShape.Abstractions;

internal class JsonDictionaryConverter<TDictionary, TKey, TValue>(JsonConverter<TKey> keyConverter, JsonConverter<TValue> valueConverter, IDictionaryShape<TDictionary, TKey, TValue> shape) : JsonConverter<TDictionary>
    where TKey : notnull
{
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
        
        JsonConverter<TKey> keyConverter = _keyConverter;
        JsonConverter<TValue> valueConverter = _valueConverter;
        
        writer.WriteStartObject();
        foreach (KeyValuePair<TKey, TValue> entry in _getDictionary(value))
        {
            keyConverter.WriteAsPropertyName(writer, entry.Key, options);
            valueConverter.Write(writer, entry.Value, options);
        }

        writer.WriteEndObject();
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
        => constructor(buffer.AsEnumerable());
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
