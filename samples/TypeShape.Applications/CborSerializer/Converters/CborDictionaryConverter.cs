using System.Formats.Cbor;

namespace TypeShape.Applications.CborSerializer.Converters;

internal class CborDictionaryConverter<TDictionary, TKey, TValue>(
    CborConverter<TKey> keyConverter, 
    CborConverter<TValue> valueConverter, 
    Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> getDictionary) : CborConverter<TDictionary>
{
    private protected readonly CborConverter<TKey> _keyConverter = keyConverter;
    private protected readonly CborConverter<TValue> _valueConverter = valueConverter;

    public override TDictionary? Read(CborReader reader)
    {
        throw new NotSupportedException($"Type {typeof(TDictionary)} does not support deserialization.");
    }

    public sealed override void Write(CborWriter writer, TDictionary? value)
    {
        if (value is null)
        {
            writer.WriteNull();
            return;
        }

        var kvEnumerable = getDictionary(value);
        int? definiteLength = kvEnumerable.TryGetNonEnumeratedCount(out int count) ? count : null;

        CborConverter<TKey> keyConverter = _keyConverter;
        CborConverter<TValue> valueConverter = _valueConverter;

        writer.WriteStartMap(definiteLength);
        foreach (KeyValuePair<TKey, TValue> kvp in kvEnumerable)
        {
            keyConverter.Write(writer, kvp.Key);
            valueConverter.Write(writer, kvp.Value);
        }

        writer.WriteEndMap();
    }
}

internal sealed class CborMutableDictionaryConverter<TDictionary, TKey, TValue>(
    CborConverter<TKey> keyConverter,
    CborConverter<TValue> valueConverter,
    Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> getDictionary,
    Func<TDictionary> createObject,
    Setter<TDictionary, KeyValuePair<TKey, TValue>> addDelegate) : CborDictionaryConverter<TDictionary, TKey, TValue>(keyConverter, valueConverter, getDictionary)
{
    private readonly Setter<TDictionary, KeyValuePair<TKey, TValue>> _addDelegate = addDelegate;

    public override TDictionary? Read(CborReader reader)
    {
        if (default(TDictionary) is null && reader.PeekState() is CborReaderState.Null)
        {
            reader.ReadNull();
            return default;
        }

        reader.ReadStartMap();
        TDictionary result = createObject();

        CborConverter<TKey> keyConverter = _keyConverter;
        CborConverter<TValue> valueConverter = _valueConverter;
        Setter<TDictionary, KeyValuePair<TKey, TValue>> addDelegate = _addDelegate;

        while (reader.PeekState() != CborReaderState.EndMap)
        {
            TKey key = keyConverter.Read(reader)!;
            TValue value = valueConverter.Read(reader)!;
            addDelegate(ref result, new(key, value));
        }

        reader.ReadEndMap();
        return result;
    }
}

internal sealed class CborImmutableDictionaryConverter<TDictionary, TKey, TValue>(
    CborConverter<TKey> keyConverter,
    CborConverter<TValue> valueConverter,
    Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> getDictionary,
    Func<IEnumerable<KeyValuePair<TKey, TValue>>, TDictionary> constructor) : CborDictionaryConverter<TDictionary, TKey, TValue>(keyConverter, valueConverter, getDictionary)
{
    public override TDictionary? Read(CborReader reader)
    {
        if (default(TDictionary) is null && reader.PeekState() is CborReaderState.Null)
        {
            reader.ReadNull();
            return default;
        }

        int? definiteLength = reader.ReadStartMap();
        List<KeyValuePair<TKey, TValue>> buffer = new(definiteLength ?? 4);
        CborConverter<TKey> keyConverter = _keyConverter;
        CborConverter<TValue> valueConverter = _valueConverter;

        while (reader.PeekState() != CborReaderState.EndMap)
        {
            TKey key = keyConverter.Read(reader)!;
            TValue value = valueConverter.Read(reader)!;
            buffer.Add(new(key, value));
        }

        reader.ReadEndMap();
        return constructor(buffer);
    }
}

