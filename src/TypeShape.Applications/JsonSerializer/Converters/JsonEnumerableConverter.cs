namespace TypeShape.Applications.JsonSerializer.Converters;

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

internal class JsonEnumerableConverter<TEnumerable, TElement> : JsonConverter<TEnumerable>
{
    private protected readonly JsonConverter<TElement> _elementConverter;
    private readonly Func<TEnumerable, IEnumerable<TElement>> _getEnumerable;

    public JsonEnumerableConverter(JsonConverter<TElement> elementConverter, Func<TEnumerable, IEnumerable<TElement>> getEnumerable)
    {
        _elementConverter = elementConverter;
        _getEnumerable = getEnumerable;
    }

    public override TEnumerable? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotSupportedException($"Deserialization not supported for type {typeof(TEnumerable)}.");
    }

    public sealed override void Write(Utf8JsonWriter writer, TEnumerable value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        JsonConverter<TElement> elementConverter = _elementConverter;

        writer.WriteStartArray();
        foreach (TElement element in _getEnumerable(value))
        {
            elementConverter.Write(writer, element, options);
        }
        writer.WriteEndArray();
    }
}

internal sealed class JsonMutableEnumerableConverter<TEnumerable, TElement> : JsonEnumerableConverter<TEnumerable, TElement>
{
    private readonly Func<TEnumerable> _createObject;
    private readonly Setter<TEnumerable, TElement> _addDelegate;

    public JsonMutableEnumerableConverter(
        JsonConverter<TElement> elementConverter, 
        Func<TEnumerable, IEnumerable<TElement>> getEnumerable, 
        Func<TEnumerable> createObject, 
        Setter<TEnumerable, TElement> addDelegate)
        : base(elementConverter, getEnumerable)
    {
        _createObject = createObject;
        _addDelegate = addDelegate;
    }

    public override TEnumerable? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (default(TEnumerable) is null && reader.TokenType is JsonTokenType.Null)
        {
            return default;
        }

        reader.EnsureTokenType(JsonTokenType.StartArray);

        TEnumerable result = _createObject();
        reader.EnsureRead();

        JsonConverter<TElement> elementConverter = _elementConverter;
        Setter<TEnumerable, TElement> addDelegate = _addDelegate;

        while (reader.TokenType != JsonTokenType.EndArray)
        {
            TElement? element = elementConverter.Read(ref reader, typeof(TElement), options);
            addDelegate(ref result, element!);
            reader.EnsureRead();
        }

        return result;
    }
}

internal sealed class JsonImmutableEnumerableConverter<TEnumerable, TElement> : JsonEnumerableConverter<TEnumerable, TElement>
{
    private readonly Func<IEnumerable<TElement>, TEnumerable> _constructor;

    public JsonImmutableEnumerableConverter(
        JsonConverter<TElement> elementConverter,
        Func<TEnumerable, IEnumerable<TElement>> getEnumerable, 
        Func<IEnumerable<TElement>, TEnumerable> constructor)
        : base(elementConverter, getEnumerable)
    {
        _constructor = constructor;
    }

    public override TEnumerable? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {   
        if (default(TEnumerable) is null && reader.TokenType is JsonTokenType.Null)
        {
            return default;
        }

        reader.EnsureTokenType(JsonTokenType.StartArray);

        List<TElement> buffer = new();
        reader.EnsureRead();

        JsonConverter<TElement> elementConverter = _elementConverter;

        while (reader.TokenType != JsonTokenType.EndArray)
        {
            TElement? element = elementConverter.Read(ref reader, typeof(TElement), options);
            buffer.Add(element!);
            reader.EnsureRead();
        }

        return _constructor(buffer);
    }
}