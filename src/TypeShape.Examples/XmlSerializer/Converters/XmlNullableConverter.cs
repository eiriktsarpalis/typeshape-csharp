using System.Xml;

namespace TypeShape.Examples.XmlSerializer.Converters;

internal sealed class XmlNullableConverter<T>(XmlConverter<T> elementConverter) : XmlConverter<T?>
    where T : struct
{
    public override T? Read(XmlReader reader)
        => reader.TryReadNullElement() ? null : elementConverter.Read(reader);

    public override void Write(XmlWriter writer, string localName, T? value)
    {
        if (value is null)
        {
            writer.WriteNullElement(localName);
        }
        else
        {
            elementConverter.Write(writer, localName, value.Value);
        }
    }
}
