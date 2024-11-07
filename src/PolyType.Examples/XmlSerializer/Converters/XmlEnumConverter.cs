using System.Xml;

namespace PolyType.Examples.XmlSerializer.Converters;

internal sealed class XmlEnumConverter<TEnum> : XmlConverter<TEnum>
    where TEnum : struct, Enum
{
    public override TEnum Read(XmlReader reader)
        => Enum.Parse<TEnum>(reader.ReadElementContentAsString());

    public override void Write(XmlWriter writer, string localName, TEnum value)
        => writer.WriteElementString(localName, value.ToString());
}