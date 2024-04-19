using System.Formats.Cbor;
using TypeShape.Abstractions;

namespace TypeShape.Applications.CborSerializer.Converters;

internal class CborEnumerableConverter<TEnumerable, TElement>(
    CborConverter<TElement> elementConverter, 
    Func<TEnumerable, IEnumerable<TElement>> getEnumerable) : CborConverter<TEnumerable>
{
    private protected readonly CborConverter<TElement> _elementConverter = elementConverter;

    public override TEnumerable? Read(CborReader reader)
    {
        throw new NotSupportedException($"Deserialization not supported for type {typeof(TEnumerable)}.");
    }

    public sealed override void Write(CborWriter writer, TEnumerable? value)
    {
        if (value is null)
        {
            writer.WriteNull();
            return;
        }

        var enumerable = getEnumerable(value);
        int? definiteLength = enumerable.TryGetNonEnumeratedCount(out int count) ? count : null;
        CborConverter<TElement> elementConverter = _elementConverter;

        writer.WriteStartArray(definiteLength);
        foreach (TElement element in enumerable)
        {
            elementConverter.Write(writer, element);
        }
        writer.WriteEndArray();
    }
}

internal sealed class CborMutableEnumerableConverter<TEnumerable, TElement>(
    CborConverter<TElement> elementConverter,
    Func<TEnumerable, IEnumerable<TElement>> getEnumerable,
    Func<TEnumerable> createObject,
    Setter<TEnumerable, TElement> addDelegate) : CborEnumerableConverter<TEnumerable, TElement>(elementConverter, getEnumerable)
{
    private readonly Setter<TEnumerable, TElement> _addDelegate = addDelegate;

    public override TEnumerable? Read(CborReader reader)
    {
        if (default(TEnumerable) is null && reader.PeekState() == CborReaderState.Null)
        {
            reader.ReadNull();
            return default;
        }

        reader.ReadStartArray();
        TEnumerable result = createObject();

        CborConverter<TElement> elementConverter = _elementConverter;
        Setter<TEnumerable, TElement> addDelegate = _addDelegate;

        while (reader.PeekState() != CborReaderState.EndArray)
        {
            TElement? element = elementConverter.Read(reader);
            addDelegate(ref result, element!);
        }

        reader.ReadEndArray();
        return result;
    }
}

internal abstract class CborImmutableEnumerableConverter<TEnumerable, TElement>(
    CborConverter<TElement> elementConverter,
    Func<TEnumerable, IEnumerable<TElement>> getEnumerable) 
    : CborEnumerableConverter<TEnumerable, TElement>(elementConverter, getEnumerable)
{
    private protected abstract TEnumerable Construct(PooledList<TElement> buffer);

    public sealed override TEnumerable? Read(CborReader reader)
    {
        if (default(TEnumerable) is null && reader.PeekState() == CborReaderState.Null)
        {
            reader.ReadNull();
            return default;
        }

        int? definiteLength = reader.ReadStartArray();
        using PooledList<TElement> buffer = new(definiteLength ?? 4);
        CborConverter<TElement> elementConverter = _elementConverter;

        while (reader.PeekState() != CborReaderState.EndArray)
        {
            TElement? element = elementConverter.Read(reader);
            buffer.Add(element!);
        }

        reader.ReadEndArray();
        return Construct(buffer);
    }
}

internal sealed class CborEnumerableConstructorEnumerableConverter<TEnumerable, TElement>(
    CborConverter<TElement> elementConverter,
    Func<TEnumerable, IEnumerable<TElement>> getEnumerable,
    Func<IEnumerable<TElement>, TEnumerable> constructor)
    : CborImmutableEnumerableConverter<TEnumerable, TElement>(elementConverter, getEnumerable)
{
    private protected override TEnumerable Construct(PooledList<TElement> buffer)
        => constructor(buffer.AsEnumerable());
}

internal sealed class CborSpanConstructorEnumerableConverter<TEnumerable, TElement>(
    CborConverter<TElement> elementConverter,
    Func<TEnumerable, IEnumerable<TElement>> getEnumerable,
    SpanConstructor<TElement, TEnumerable> constructor)
    : CborImmutableEnumerableConverter<TEnumerable, TElement>(elementConverter, getEnumerable)
{
    private protected override TEnumerable Construct(PooledList<TElement> buffer)
        => constructor(buffer.AsSpan());
}