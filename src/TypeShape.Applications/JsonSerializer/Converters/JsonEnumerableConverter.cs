namespace TypeShape.Applications.JsonSerializer.Converters;

using System.Text.Json;
using System.Text.Json.Serialization;
using TypeShape.Applications.Common;

internal class JsonEnumerableConverter<TEnumerable, TElement>(JsonConverter<TElement> elementConverter, IEnumerableShape<TEnumerable, TElement> shape) : JsonConverter<TEnumerable>
{
    private protected readonly JsonConverter<TElement> _elementConverter = elementConverter;
    private readonly IIterator<TEnumerable, TElement> _iterator = Iterator.Create(shape);

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

        writer.WriteStartArray();

        (Utf8JsonWriter, JsonConverter<TElement>, JsonSerializerOptions) state = (writer, _elementConverter, options);
        _iterator.Iterate(value, WriteElement, ref state);

        writer.WriteEndArray();

        static void WriteElement(TElement element, ref (Utf8JsonWriter writer, JsonConverter<TElement> converter, JsonSerializerOptions options) state)
        {
            state.converter.Write(state.writer, element, state.options);
        }
    }
}

internal sealed class JsonMutableEnumerableConverter<TEnumerable, TElement>(
    JsonConverter<TElement> elementConverter,
    IEnumerableShape<TEnumerable, TElement> shape,
    Func<TEnumerable> createObject,
    Setter<TEnumerable, TElement> addDelegate) : JsonEnumerableConverter<TEnumerable, TElement>(elementConverter, shape)
{
    private readonly Setter<TEnumerable, TElement> _addDelegate = addDelegate;

    public override TEnumerable? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (default(TEnumerable) is null && reader.TokenType is JsonTokenType.Null)
        {
            return default;
        }

        reader.EnsureTokenType(JsonTokenType.StartArray);

        TEnumerable result = createObject();
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

internal sealed class JsonImmutableEnumerableConverter<TEnumerable, TElement>(
    JsonConverter<TElement> elementConverter,
    IEnumerableShape<TEnumerable, TElement> shape,
    Func<IEnumerable<TElement>, TEnumerable> constructor) : JsonEnumerableConverter<TEnumerable, TElement>(elementConverter, shape)
{
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

        return constructor(buffer);
    }
}