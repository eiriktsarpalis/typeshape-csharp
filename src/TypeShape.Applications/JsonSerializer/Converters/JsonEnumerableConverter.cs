namespace TypeShape.Applications.JsonSerializer.Converters;

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

internal class JsonEnumerableConverter<TEnumerable, TElement> : JsonConverter<TEnumerable>
{
    public Func<TEnumerable, IEnumerable<TElement>>? GetEnumerable { get; set; }
    public JsonConverter<TElement>? ElementConverter { get; set; }

    public override TEnumerable? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotSupportedException($"Deserialization not supported for type {typeof(TEnumerable)}.");
    }

    public sealed override void Write(Utf8JsonWriter writer, TEnumerable value, JsonSerializerOptions options)
    {
        Debug.Assert(GetEnumerable != null);
        Debug.Assert(ElementConverter != null);

        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        JsonConverter<TElement> elementConverter = ElementConverter;

        writer.WriteStartArray();
        foreach (TElement element in GetEnumerable(value))
        {
            elementConverter.Write(writer, element, options);
        }
        writer.WriteEndArray();
    }
}

internal sealed class JsonMutableEnumerableConverter<TEnumerable, TElement> : JsonEnumerableConverter<TEnumerable, TElement>
{
    public Func<TEnumerable>? CreateObject { get; set; }
    public Setter<TEnumerable, TElement>? AddDelegate { get; set; }

    public override TEnumerable? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        Debug.Assert(CreateObject != null);
        Debug.Assert(ElementConverter != null);
        Debug.Assert(AddDelegate != null);

        if (default(TEnumerable) is null && reader.TokenType is JsonTokenType.Null)
        {
            return default;
        }

        reader.EnsureTokenType(JsonTokenType.StartArray);

        TEnumerable result = CreateObject();
        reader.EnsureRead();

        JsonConverter<TElement> elementConverter = ElementConverter;
        Setter<TEnumerable, TElement> addDelegate = AddDelegate;

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
    public Func<IEnumerable<TElement>, TEnumerable>? Constructor { get; set; }

    public override TEnumerable? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        Debug.Assert(Constructor != null);
        Debug.Assert(ElementConverter != null);
        
        if (default(TEnumerable) is null && reader.TokenType is JsonTokenType.Null)
        {
            return default;
        }

        reader.EnsureTokenType(JsonTokenType.StartArray);

        List<TElement> buffer = new();
        reader.EnsureRead();

        JsonConverter<TElement> elementConverter = ElementConverter;

        while (reader.TokenType != JsonTokenType.EndArray)
        {
            TElement? element = elementConverter.Read(ref reader, typeof(TElement), options);
            buffer.Add(element!);
            reader.EnsureRead();
        }

        return Constructor(buffer);
    }
}