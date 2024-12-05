using System.Xml;

namespace PolyType.Examples.XmlSerializer.Converters;

internal sealed class XmlEnumConverter<TEnum> : XmlConverter<TEnum>
    where TEnum : struct, Enum
{
    public override TEnum Read(XmlReader reader)
#if NET
        => Enum.Parse<TEnum>(reader.ReadElementContentAsString());
#else
        => (TEnum)Enum.Parse(typeof(TEnum), reader.ReadElementContentAsString());
#endif

    public override void Write(XmlWriter writer, string localName, TEnum value)
        => writer.WriteElementString(localName, value.ToString());
}