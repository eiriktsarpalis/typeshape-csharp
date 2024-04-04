namespace TypeShape.Applications.JsonSerializer.Converters;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeShape.Applications.Common;

internal class JsonEnumerableConverter<TEnumerable, TElement>(JsonConverter<TElement> elementConverter, IEnumerableTypeShape<TEnumerable, TElement> typeShape) : JsonConverter<TEnumerable>
{
    private protected readonly JsonConverter<TElement> _elementConverter = elementConverter;
    private readonly IIterator<TEnumerable, TElement> _iterator = Iterator.Create(typeShape);

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
    IEnumerableTypeShape<TEnumerable, TElement> typeShape,
    Func<TEnumerable> createObject,
    Setter<TEnumerable, TElement> addDelegate) : JsonEnumerableConverter<TEnumerable, TElement>(elementConverter, typeShape)
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

internal abstract class JsonImmutableEnumerableConverter<TEnumerable, TElement>(
    JsonConverter<TElement> elementConverter,
    IEnumerableTypeShape<TEnumerable, TElement> typeShape)
    : JsonEnumerableConverter<TEnumerable, TElement>(elementConverter, typeShape)
{
    private protected abstract TEnumerable Construct(PooledList<TElement> buffer);
    public sealed override TEnumerable? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {   
        if (default(TEnumerable) is null && reader.TokenType is JsonTokenType.Null)
        {
            return default;
        }

        reader.EnsureTokenType(JsonTokenType.StartArray);
        reader.EnsureRead();

        using PooledList<TElement> buffer = new();
        JsonConverter<TElement> elementConverter = _elementConverter;

        while (reader.TokenType != JsonTokenType.EndArray)
        {
            TElement? element = elementConverter.Read(ref reader, typeof(TElement), options);
            buffer.Add(element!);
            reader.EnsureRead();
        }

        return Construct(buffer);
    }
}

internal sealed class JsonEnumerableConstructorEnumerableConverter<TEnumerable, TElement>(
    JsonConverter<TElement> elementConverter,
    IEnumerableTypeShape<TEnumerable, TElement> typeShape,
    Func<IEnumerable<TElement>, TEnumerable> enumerableConstructor) 
    : JsonImmutableEnumerableConverter<TEnumerable, TElement>(elementConverter, typeShape)
{
    private protected override TEnumerable Construct(PooledList<TElement> buffer)
        => enumerableConstructor(buffer.AsEnumerable());
}

internal sealed class JsonSpanConstructorEnumerableConverter<TEnumerable, TElement>(
    JsonConverter<TElement> elementConverter,
    IEnumerableTypeShape<TEnumerable, TElement> typeShape,
    SpanConstructor<TElement, TEnumerable> spanConstructor) 
    : JsonImmutableEnumerableConverter<TEnumerable, TElement>(elementConverter, typeShape)
{
    private protected override TEnumerable Construct(PooledList<TElement> buffer)
        => spanConstructor(buffer.AsSpan());
}

internal sealed class Json2DArrayConverter<TElement>(JsonConverter<TElement> elementConverter) : JsonConverter<TElement[,]>
{
    public override TElement[,]? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType is JsonTokenType.Null)
        {
            return null;
        }

        reader.EnsureTokenType(JsonTokenType.StartArray);
        reader.EnsureRead();

        using PooledList<TElement> buffer = new();
        int rows = 0;
        int? columns = null;

        while (reader.TokenType != JsonTokenType.EndArray)
        {
            reader.EnsureTokenType(JsonTokenType.StartArray);
            reader.EnsureRead();

            int rowLength = 0;
            while (reader.TokenType != JsonTokenType.EndArray)
            {
                TElement? element = elementConverter.Read(ref reader, typeof(TElement), options);
                buffer.Add(element!);
                reader.EnsureRead();
                rowLength++;
            }

            reader.EnsureRead();
            rows++;

            if ((columns ??= rowLength) != rowLength)
            {
                JsonHelpers.ThrowJsonException("The deserialized jagged array must be rectangular.");
            }
        }

        TElement[,] result = new TElement[rows, columns ?? 0];
        Span<TElement> destination = MemoryMarshal.CreateSpan(ref Unsafe.As<byte, TElement>(ref MemoryMarshal.GetArrayDataReference(result)), result.Length);
        buffer.AsSpan().CopyTo(destination);
        return result;
    }

    public override void Write(Utf8JsonWriter writer, TElement[,] value, JsonSerializerOptions options)
    {
        int n = value.GetLength(0);
        int m = value.GetLength(1);

        writer.WriteStartArray();
        for (int i = 0; i < n; i++)
        {
            writer.WriteStartArray();
            for (int j = 0; j < m; j++)
            {
                elementConverter.Write(writer, value[i, j], options);
            }

            writer.WriteEndArray();
        }

        writer.WriteEndArray();
    }
}
