using System.Xml;
using TypeShape.Applications.Common;

namespace TypeShape.Applications.XmlSerializer.Converters;

internal class XmlEnumerableConverter<TEnumerable, TElement>(
    XmlConverter<TElement> elementConverter,
    Func<TEnumerable, IEnumerable<TElement>> getEnumerable)
    : XmlConverter<TEnumerable>
{
    private protected readonly XmlConverter<TElement> _elementConverter = elementConverter;
    private readonly Func<TEnumerable, IEnumerable<TElement>> _getEnumerable = getEnumerable;

    public override TEnumerable? Read(XmlReader reader)
    {
        throw new NotSupportedException($"Deserialization not supported for type {typeof(TEnumerable)}.");
    }

    public override void Write(XmlWriter writer, string localName, TEnumerable? value)
    {
        if (value is null)
        {
            writer.WriteNullElement(localName);
            return;
        }

        XmlConverter<TElement> converter = _elementConverter;

        writer.WriteStartElement(localName);
        foreach (TElement element in _getEnumerable(value))
        {
            _elementConverter.Write(writer, "element", element);
        }
        writer.WriteEndElement();
    }
}

internal sealed class XmlMutableEnumerableConverter<TEnumerable, TElement>(
    XmlConverter<TElement> elementConverter,
    Func<TEnumerable, IEnumerable<TElement>> getEnumerable,
    Func<TEnumerable> createObject,
    Setter<TEnumerable, TElement> addDelegate)
    : XmlEnumerableConverter<TEnumerable, TElement>(elementConverter, getEnumerable)
{
    private readonly Setter<TEnumerable, TElement> _addDelegate = addDelegate;

    public override TEnumerable? Read(XmlReader reader)
    {
        if (default(TEnumerable) is null && reader.TryReadNullElement())
        {
            return default;
        }

        TEnumerable result = createObject();

        if (reader.IsEmptyElement)
        {
            reader.ReadStartElement();
            return result;
        }

        reader.ReadStartElement();
        XmlConverter<TElement> elementConverter = _elementConverter;
        Setter<TEnumerable, TElement> addDelegate = _addDelegate;

        while (reader.NodeType != XmlNodeType.EndElement)
        {
            if (reader.NodeType != XmlNodeType.Element)
            {
                continue;
            }

            TElement? element = elementConverter.Read(reader);
            addDelegate(ref result, element!);
        }

        reader.ReadEndElement();
        return result;
    }
}

internal sealed class XmlImmutableEnumerableConverter<TEnumerable, TElement>(
    XmlConverter<TElement> elementConverter,
    Func<TEnumerable, IEnumerable<TElement>> getEnumerable,
    Func<IEnumerable<TElement>, TEnumerable> constructor)
    : XmlEnumerableConverter<TEnumerable, TElement>(elementConverter, getEnumerable)
{
    public override TEnumerable? Read(XmlReader reader)
    {
        if (default(TEnumerable) is null && reader.TryReadNullElement())
        {
            return default;
        }

        if (reader.IsEmptyElement)
        {
            reader.ReadStartElement();
            return constructor([]);
        }

        XmlConverter<TElement> elementConverter = _elementConverter;
        List<TElement> elements = [];

        reader.ReadStartElement();
        while (reader.NodeType != XmlNodeType.EndElement)
        {
            if (reader.NodeType != XmlNodeType.Element)
            {
                continue;
            }

            TElement? element = elementConverter.Read(reader);
            elements.Add(element!);
        }

        reader.ReadEndElement();
        return constructor(elements);
    }
}
