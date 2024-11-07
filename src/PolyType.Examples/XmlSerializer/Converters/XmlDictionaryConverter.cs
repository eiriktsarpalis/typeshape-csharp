using System.Xml;
using PolyType.Abstractions;
using PolyType.Examples.Utilities;

namespace PolyType.Examples.XmlSerializer.Converters;

internal class XmlDictionaryConverter<TDictionary, TKey, TValue>(
    XmlConverter<TKey> keyConverter, 
    XmlConverter<TValue> valueConverter, 
    Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> getEnumerable) 
    : XmlConverter<TDictionary>
    where TKey : notnull
{
    private protected readonly XmlConverter<TKey> _keyConverter = keyConverter;
    private protected readonly XmlConverter<TValue> _valueConverter = valueConverter;
    private readonly Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> _getEnumerable = getEnumerable;

    public override TDictionary? Read(XmlReader reader)
        => throw new NotSupportedException($"Type {typeof(TDictionary)} does not support deserialization.");

    public sealed override void Write(XmlWriter writer, string localName, TDictionary? value)
    {
        if (value is null)
        {
            writer.WriteNullElement(localName);
            return;
        }

        XmlConverter<TKey> keyConverter = _keyConverter;
        XmlConverter<TValue> valueConverter = _valueConverter;

        writer.WriteStartElement(localName);
        foreach (KeyValuePair<TKey, TValue> entry in _getEnumerable(value))
        {
            writer.WriteStartElement("entry");
            keyConverter.Write(writer, "key", entry.Key);
            valueConverter.Write(writer, "value", entry.Value);
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
    }
}

internal sealed class XmlMutableDictionaryConverter<TDictionary, TKey, TValue>(
    XmlConverter<TKey> keyConverter,
    XmlConverter<TValue> valueConverter,
    Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> getEnumerable,
    Func<TDictionary> createObject,
    Setter<TDictionary, KeyValuePair<TKey, TValue>> addDelegate)
    : XmlDictionaryConverter<TDictionary, TKey, TValue>(keyConverter, valueConverter, getEnumerable)
    where TKey : notnull
{
    private readonly Setter<TDictionary, KeyValuePair<TKey, TValue>> _addDelegate = addDelegate;

    public override TDictionary? Read(XmlReader reader)
    {
        if (default(TDictionary) is null && reader.TryReadNullElement())
        {
            return default;
        }

        TDictionary result = createObject();

        if (reader.IsEmptyElement)
        {
            reader.Read();
            return result;
        }

        XmlConverter<TKey> keyConverter = _keyConverter;
        XmlConverter<TValue> valueConverter = _valueConverter;
        Setter<TDictionary, KeyValuePair<TKey, TValue>> addDelegate = _addDelegate;
        
        reader.ReadStartElement();
        while (reader.NodeType != XmlNodeType.EndElement)
        {
            if (reader.NodeType != XmlNodeType.Element)
            {
                continue;
            }

            reader.ReadStartElement();
            TKey key = keyConverter.Read(reader)!;
            TValue value = valueConverter.Read(reader)!;
            addDelegate(ref result, new(key, value));
            reader.ReadEndElement();
        }

        reader.ReadEndElement();
        return result;
    }
}

internal abstract class XmlImmutableDictionaryConverter<TDictionary, TKey, TValue>(
    XmlConverter<TKey> keyConverter,
    XmlConverter<TValue> valueConverter,
    Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> getEnumerable)
    : XmlDictionaryConverter<TDictionary, TKey, TValue>(keyConverter, valueConverter, getEnumerable)
    where TKey : notnull
{
    private protected abstract TDictionary Construct(PooledList<KeyValuePair<TKey, TValue>> buffer);
    public sealed override TDictionary? Read(XmlReader reader)
    {
        if (default(TDictionary) is null && reader.TryReadNullElement())
        {
            return default;
        }

        using PooledList<KeyValuePair<TKey, TValue>> buffer = new();

        if (reader.IsEmptyElement)
        {
            reader.Read();
            return Construct(buffer);
        }

        XmlConverter<TKey> keyConverter = _keyConverter;
        XmlConverter<TValue> valueConverter = _valueConverter;

        reader.ReadStartElement();
        while (reader.NodeType != XmlNodeType.EndElement)
        {
            if (reader.NodeType != XmlNodeType.Element)
            {
                continue;
            }

            reader.ReadStartElement();
            TKey key = keyConverter.Read(reader)!;
            TValue value = valueConverter.Read(reader)!;
            buffer.Add(new(key, value));
            reader.ReadEndElement();
        }

        reader.ReadEndElement();
        return Construct(buffer);
    }
}

internal sealed class XmlEnumerableConstructorDictionaryConverter<TDictionary, TKey, TValue>(
    XmlConverter<TKey> keyConverter,
    XmlConverter<TValue> valueConverter,
    Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> getEnumerable,
    Func<IEnumerable<KeyValuePair<TKey, TValue>>, TDictionary> constructor)
    : XmlImmutableDictionaryConverter<TDictionary, TKey, TValue>(keyConverter, valueConverter, getEnumerable)
    where TKey : notnull
{
    private protected override TDictionary Construct(PooledList<KeyValuePair<TKey, TValue>> buffer)
        => constructor(buffer.ExchangeToArraySegment());
}

internal sealed class XmlSpanConstructorDictionaryConverter<TDictionary, TKey, TValue>(
    XmlConverter<TKey> keyConverter,
    XmlConverter<TValue> valueConverter,
    Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> getEnumerable,
    SpanConstructor<KeyValuePair<TKey, TValue>, TDictionary> constructor)
    : XmlImmutableDictionaryConverter<TDictionary, TKey, TValue>(keyConverter, valueConverter, getEnumerable)
    where TKey : notnull
{
    private protected override TDictionary Construct(PooledList<KeyValuePair<TKey, TValue>> buffer)
        => constructor(buffer.AsSpan());
}