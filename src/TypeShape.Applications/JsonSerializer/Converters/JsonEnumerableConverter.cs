using System.Diagnostics;

namespace TypeShape.Applications.JsonSerializer.Converters;

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeShape.Abstractions;
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

internal sealed class JsonMDArrayConverter<TArray, TElement>(JsonConverter<TElement> elementConverter, int rank) : JsonConverter<TArray>
{
    [ThreadStatic] private static int[]? _dimensions;

    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "The Array.CreateInstance method generates TArray instances.")]
    public override TArray? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType is JsonTokenType.Null)
        {
            return default;
        }

        int[] dimensions = _dimensions ??= new int[rank];
        dimensions.AsSpan().Fill(-1);
        PooledList<TElement> buffer = new();
        try
        {
            ReadSubArray(ref reader, ref buffer, dimensions, options);
            dimensions.AsSpan().Replace(-1, 0);
            Array result = Array.CreateInstance(typeof(TElement), dimensions);
            buffer.AsSpan().CopyTo(AsSpan(result));
            return (TArray)(object)result;
        }
        finally
        {
            buffer.Dispose();
        }
    }

    public override void Write(Utf8JsonWriter writer, TArray value, JsonSerializerOptions options)
    {
        var array = (Array)(object)value!;
        Debug.Assert(rank == array.Rank);

        int[] dimensions = _dimensions ??= new int[rank];
        for (int i = 0; i < rank; i++) dimensions[i] = array.GetLength(i);
        WriteSubArray(writer, dimensions, AsSpan(array), options);
    }

    private void ReadSubArray(
        ref Utf8JsonReader reader,
        ref PooledList<TElement> buffer,
        Span<int> dimensions,
        JsonSerializerOptions options)
    {
        Debug.Assert(dimensions.Length > 0);
        reader.EnsureTokenType(JsonTokenType.StartArray);
        reader.EnsureRead();
        
        int dimension = 0;
        while (reader.TokenType != JsonTokenType.EndArray)
        {
            if (dimensions.Length > 1)
            {
                ReadSubArray(ref reader, ref buffer, dimensions[1..], options);
            }
            else
            {
                TElement? element = elementConverter.Read(ref reader, typeof(TElement), options);
                buffer.Add(element!);
            }
            
            reader.EnsureRead();
            dimension++;
        }
        
        if (dimensions[0] < 0)
        {
            dimensions[0] = dimension;
        }
        else if (dimensions[0] != dimension)
        {
            JsonHelpers.ThrowJsonException("The deserialized jagged array was not rectangular.");
        }
    }
    
    private void WriteSubArray(
        Utf8JsonWriter writer,
        ReadOnlySpan<int> dimensions,
        ReadOnlySpan<TElement> elements,
        JsonSerializerOptions options)
    {
        Debug.Assert(dimensions.Length > 0);
        
        writer.WriteStartArray();

        int outerDim = dimensions[0];
        if (dimensions.Length > 1 && outerDim > 0)
        {
            int subArrayLength = elements.Length / outerDim;
            for (int i = 0; i < outerDim; i++)
            {
                WriteSubArray(writer, dimensions[1..], elements[..subArrayLength], options);
                elements = elements[subArrayLength..];
            }
        }
        else
        {
            for (int i = 0; i < outerDim; i++)
            {
                elementConverter.Write(writer, elements[i], options);
            }
        }
            
        writer.WriteEndArray();
    }
    
    private static Span<TElement> AsSpan(Array array) =>
        MemoryMarshal.CreateSpan(
            ref Unsafe.As<byte, TElement>(ref MemoryMarshal.GetArrayDataReference(array)), 
            array.Length);
}
