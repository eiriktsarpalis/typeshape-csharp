using System.Diagnostics;
using System.Xml;

namespace TypeShape.Applications.XmlSerializer.Converters;

internal sealed class DelayedXmlConverter<T>(ResultHolder<XmlConverter<T>> holder) : XmlConverter<T>
{
    public XmlConverter<T> Underlying
    {
        get
        {
            Debug.Assert(holder.Value != null);
            return holder.Value;
        }
    }

    public override T? Read(XmlReader reader)
        => Underlying.Read(reader);

    public override void Write(XmlWriter writer, string localName, T? value)
        => Underlying.Write(writer, localName, value);
}
