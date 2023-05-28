using System.Formats.Cbor;

namespace TypeShape.Applications.CborSerializer.Converters;

internal class CborEnumerableConverter<TEnumerable, TElement> : CborConverter<TEnumerable>
{
    private protected readonly CborConverter<TElement> _elementConverter;
    private readonly Func<TEnumerable, IEnumerable<TElement>> _getEnumerable;

    public CborEnumerableConverter(CborConverter<TElement> elementConverter, Func<TEnumerable, IEnumerable<TElement>> getEnumerable)
    {
        _elementConverter = elementConverter;
        _getEnumerable = getEnumerable;
    }

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

        var enumerable = _getEnumerable(value);
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

internal sealed class CborMutableEnumerableConverter<TEnumerable, TElement> : CborEnumerableConverter<TEnumerable, TElement>
{
    private readonly Func<TEnumerable> _createObject;
    private readonly Setter<TEnumerable, TElement> _addDelegate;

    public CborMutableEnumerableConverter(
        CborConverter<TElement> elementConverter,
        Func<TEnumerable, IEnumerable<TElement>> getEnumerable,
        Func<TEnumerable> createObject,
        Setter<TEnumerable, TElement> addDelegate)
        : base(elementConverter, getEnumerable)
    {
        _createObject = createObject;
        _addDelegate = addDelegate;
    }

    public override TEnumerable? Read(CborReader reader)
    {
        if (default(TEnumerable) is null && reader.PeekState() == CborReaderState.Null)
        {
            reader.ReadNull();
            return default;
        }

        reader.ReadStartArray();
        TEnumerable result = _createObject();

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

internal sealed class CborImmutableEnumerableConverter<TEnumerable, TElement> : CborEnumerableConverter<TEnumerable, TElement>
{
    private readonly Func<IEnumerable<TElement>, TEnumerable> _constructor;

    public CborImmutableEnumerableConverter(
        CborConverter<TElement> elementConverter,
        Func<TEnumerable, IEnumerable<TElement>> getEnumerable,
        Func<IEnumerable<TElement>, TEnumerable> constructor)
        : base(elementConverter, getEnumerable)
    {
        _constructor = constructor;
    }

    public override TEnumerable? Read(CborReader reader)
    {
        if (default(TEnumerable) is null && reader.PeekState() == CborReaderState.Null)
        {
            reader.ReadNull();
            return default;
        }

        int? definiteLength = reader.ReadStartArray();
        List<TElement?> buffer = new(definiteLength ?? 4);
        CborConverter<TElement> elementConverter = _elementConverter;

        while (reader.PeekState() != CborReaderState.EndArray)
        {
            TElement? element = elementConverter.Read(reader);
            buffer.Add(element);
        }

        reader.ReadEndArray();
        return _constructor(buffer!);
    }
}