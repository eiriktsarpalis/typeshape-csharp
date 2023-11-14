using System.Xml;

namespace TypeShape.Applications.XmlSerializer;

public abstract class XmlConverter
{
    internal XmlConverter() { }
    public abstract Type Type { get; }
}

public abstract class XmlConverter<T> : XmlConverter
{
    public sealed override Type Type => typeof(T);
    public abstract void Write(XmlWriter writer, string localName, T? value);
    public abstract T? Read(XmlReader reader);
}
