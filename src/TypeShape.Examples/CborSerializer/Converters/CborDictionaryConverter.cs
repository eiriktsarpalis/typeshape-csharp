using System.Formats.Cbor;
using TypeShape.Abstractions;
using TypeShape.Examples.Utilities;

namespace TypeShape.Examples.CborSerializer.Converters;

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

        IReadOnlyDictionary<TKey, TValue> dictionary = getDictionary(value);
        CborConverter<TKey> keyConverter = _keyConverter;
        CborConverter<TValue> valueConverter = _valueConverter;

        writer.WriteStartMap(dictionary.Count);
        foreach (KeyValuePair<TKey, TValue> kvp in dictionary)
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

internal abstract class CborImmutableDictionaryConverter<TDictionary, TKey, TValue>(
    CborConverter<TKey> keyConverter,
    CborConverter<TValue> valueConverter,
    Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> getDictionary)
    : CborDictionaryConverter<TDictionary, TKey, TValue>(keyConverter, valueConverter, getDictionary)
{
    private protected abstract TDictionary Construct(PooledList<KeyValuePair<TKey, TValue>> buffer);
    public sealed override TDictionary? Read(CborReader reader)
    {
        if (default(TDictionary) is null && reader.PeekState() is CborReaderState.Null)
        {
            reader.ReadNull();
            return default;
        }

        int? definiteLength = reader.ReadStartMap();
        using PooledList<KeyValuePair<TKey, TValue>> buffer = new(definiteLength ?? 4);
        CborConverter<TKey> keyConverter = _keyConverter;
        CborConverter<TValue> valueConverter = _valueConverter;

        while (reader.PeekState() != CborReaderState.EndMap)
        {
            TKey key = keyConverter.Read(reader)!;
            TValue value = valueConverter.Read(reader)!;
            buffer.Add(new(key, value));
        }

        reader.ReadEndMap();
        return Construct(buffer);
    }
}

internal sealed class CborEnumerableConstructorDictionaryConverter<TDictionary, TKey, TValue>(
    CborConverter<TKey> keyConverter,
    CborConverter<TValue> valueConverter,
    Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> getDictionary,
    Func<IEnumerable<KeyValuePair<TKey, TValue>>, TDictionary> constructor) 
    : CborImmutableDictionaryConverter<TDictionary, TKey, TValue>(keyConverter, valueConverter, getDictionary)
{
    private protected override TDictionary Construct(PooledList<KeyValuePair<TKey, TValue>> buffer)
        => constructor(buffer.ExchangeToArraySegment());
}

internal sealed class CborSpanConstructorDictionaryConverter<TDictionary, TKey, TValue>(
    CborConverter<TKey> keyConverter,
    CborConverter<TValue> valueConverter,
    Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> getDictionary,
    SpanConstructor<KeyValuePair<TKey, TValue>, TDictionary> constructor) 
    : CborImmutableDictionaryConverter<TDictionary, TKey, TValue>(keyConverter, valueConverter, getDictionary)
{
    private protected override TDictionary Construct(PooledList<KeyValuePair<TKey, TValue>> buffer)
        => constructor(buffer.AsSpan());
}
