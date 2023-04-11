namespace TypeShape.Applications.JsonSerializer.Converters;

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

internal sealed class JsonEnumerableConverter<TEnumerable, TElement> : JsonConverter<TEnumerable>
{
    internal JsonConverter<TElement>? ElementConverter { get; set; }
    internal Func<TEnumerable, IEnumerable<TElement>>? GetEnumerable { get; set; }
    internal Func<TEnumerable>? CreateObject { get; set; }
    internal Setter<TEnumerable, TElement>? AddDelegate { get; set; }

    public override TEnumerable? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (default(TEnumerable) is null && reader.TokenType is JsonTokenType.Null)
        {
            return default;
        }

        Func<TEnumerable>? createObject = CreateObject;
        if (createObject is null)
        {
            ThrowNotSupportedException();
            [DoesNotReturn] static void ThrowNotSupportedException() => throw new NotSupportedException($"Deserialization not supported for type {typeof(TEnumerable)}.");
        }

        reader.EnsureTokenType(JsonTokenType.StartArray);

        TEnumerable result = createObject();
        reader.EnsureRead();

        Debug.Assert(ElementConverter != null);
        Debug.Assert(AddDelegate != null);
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

    public override void Write(Utf8JsonWriter writer, TEnumerable value, JsonSerializerOptions options)
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

internal sealed class JsonArrayConverter<TElement> : JsonConverter<TElement[]>
{
    internal JsonConverter<TElement>? ElementConverter { get; set; }

    public override TElement[]? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType is JsonTokenType.Null)
        {
            return null;
        }

        reader.EnsureTokenType(JsonTokenType.StartArray);
        reader.EnsureRead();

        Debug.Assert(ElementConverter != null);
        JsonConverter<TElement> elementConverter = ElementConverter;
        var buffer = new List<TElement>();

        while (reader.TokenType != JsonTokenType.EndArray)
        {
            TElement? element = elementConverter.Read(ref reader, typeof(TElement), options);
            buffer.Add(element!);
            reader.EnsureRead();
        }

        return buffer.ToArray();
    }

    public override void Write(Utf8JsonWriter writer, TElement[] array, JsonSerializerOptions options)
    {
        Debug.Assert(ElementConverter != null);

        if (array is null)
        {
            writer.WriteNullValue();
            return;
        }

        JsonConverter<TElement> elementConverter = ElementConverter;

        writer.WriteStartArray();
        foreach (TElement element in array)
        {
            elementConverter.Write(writer, element, options);
        }
        writer.WriteEndArray();
    }
}
