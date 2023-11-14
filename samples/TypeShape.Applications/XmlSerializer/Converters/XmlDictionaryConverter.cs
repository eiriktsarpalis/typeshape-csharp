using System.Xml;
using TypeShape.Applications.Common;

namespace TypeShape.Applications.XmlSerializer.Converters;

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

internal sealed class XmlImmutableDictionaryConverter<TDictionary, TKey, TValue>(
    XmlConverter<TKey> keyConverter,
    XmlConverter<TValue> valueConverter,
    Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> getEnumerable,
    Func<IEnumerable<KeyValuePair<TKey, TValue>>, TDictionary> constructor)
    : XmlDictionaryConverter<TDictionary, TKey, TValue>(keyConverter, valueConverter, getEnumerable)
    where TKey : notnull
{
    public override TDictionary? Read(XmlReader reader)
    {
        if (default(TDictionary) is null && reader.TryReadNullElement())
        {
            return default;
        }

        if (reader.IsEmptyElement)
        {
            reader.Read();
            return constructor([]);
        }

        XmlConverter<TKey> keyConverter = _keyConverter;
        XmlConverter<TValue> valueConverter = _valueConverter;
        List<KeyValuePair<TKey, TValue>> buffer = [];

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
        return constructor(buffer);
    }
}