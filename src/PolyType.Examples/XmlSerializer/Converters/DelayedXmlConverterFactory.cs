using PolyType.Abstractions;
using PolyType.Utilities;
using System.Xml;

namespace PolyType.Examples.XmlSerializer.Converters;

internal sealed class DelayedXmlConverterFactory : IDelayedValueFactory
{
    public DelayedValue Create<T>(ITypeShape<T> typeShape) =>
        new DelayedValue<XmlConverter<T>>(self => new DelayedXmlConverter<T>(self));

    private sealed class DelayedXmlConverter<T>(DelayedValue<XmlConverter<T>> self) : XmlConverter<T>
    {
        public override T? Read(XmlReader reader) =>
            self.Result.Read(reader);

        public override void Write(XmlWriter writer, string localName, T? value) =>
            self.Result.Write(writer, localName, value);
    }
}
