namespace TypeShape.Applications.JsonSerializer.Converters;

using System.Text.Json;
using System.Text.Json.Serialization;
using TypeShape.Applications.Common;

internal class JsonEnumerableConverter<TEnumerable, TElement> : JsonConverter<TEnumerable>
{
    private protected readonly JsonConverter<TElement> _elementConverter;
    private readonly IIterator<TEnumerable, TElement> _iterator;

    public JsonEnumerableConverter(JsonConverter<TElement> elementConverter, IEnumerableShape<TEnumerable, TElement> shape)
    {
        _elementConverter = elementConverter;
        _iterator = Iterator.Create(shape);
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

internal sealed class JsonMutableEnumerableConverter<TEnumerable, TElement> : JsonEnumerableConverter<TEnumerable, TElement>
{
    private readonly Func<TEnumerable> _createObject;
    private readonly Setter<TEnumerable, TElement> _addDelegate;

    public JsonMutableEnumerableConverter(
        JsonConverter<TElement> elementConverter,
        IEnumerableShape<TEnumerable, TElement> shape,
        Func<TEnumerable> createObject, 
        Setter<TEnumerable, TElement> addDelegate)
        : base(elementConverter, shape)
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
        IEnumerableShape<TEnumerable, TElement> shape,
        Func<IEnumerable<TElement>, TEnumerable> constructor)
        : base(elementConverter, shape)
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