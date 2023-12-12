using System.Diagnostics;
using System.Xml;
using TypeShape.Applications.XmlSerializer.Converters;

namespace TypeShape.Applications.XmlSerializer;

public static partial class XmlSerializer
{
    private static readonly XmlWriterSettings s_writerSettings = new()
    {
        NamespaceHandling = NamespaceHandling.Default,
        Indent = true,
    };

    private static readonly XmlReaderSettings s_readerSettings = new()
    {
        ConformanceLevel = ConformanceLevel.Auto,
        IgnoreWhitespace = true,
    };

    public static XmlConverter<T> CreateConverter<T>(ITypeShape<T> shape)
    {
        var visitor = new Visitor();
        return (XmlConverter<T>)shape.Accept(visitor, null)!;
    }

    public static string Serialize<T>(this XmlConverter<T> converter, T? value, XmlWriterSettings? settings = null)
    {
        using var sw = new StringWriter();
        using var writer = XmlWriter.Create(sw, settings ?? s_writerSettings);
        converter.Write(writer, localName: "root", value);
        writer.Flush();
        return sw.ToString();
    }

    public static T? Deserialize<T>(this XmlConverter<T> converter, string xml, XmlReaderSettings? settings = null)
    {
        using var sr = new StringReader(xml);
        using var reader = XmlReader.Create(sr, settings ?? s_readerSettings);

        do
        {
            reader.EnsureRead();
        } while (reader.NodeType != XmlNodeType.Element);

        T? result = converter.Read(reader);
        Debug.Assert(reader.Depth == 0);
        return result;
    }

    public static string Serialize<T>(T? value) where T : ITypeShapeProvider<T>
        => XmlSerializerCache<T, T>.Value.Serialize(value);

    public static T? Deserialize<T>(string xml) where T : ITypeShapeProvider<T>
        => XmlSerializerCache<T, T>.Value.Deserialize(xml);

    public static string Serialize<T, TPRovider>(T? value) where TPRovider : ITypeShapeProvider<T>
        => XmlSerializerCache<T, TPRovider>.Value.Serialize(value);

    public static T? Deserialize<T, TPRovider>(string xml) where TPRovider : ITypeShapeProvider<T>
        => XmlSerializerCache<T, TPRovider>.Value.Deserialize(xml);

    private static class XmlSerializerCache<T, TProvider> where TProvider : ITypeShapeProvider<T>
    {
        public static XmlConverter<T> Value => s_value ??= CreateConverter(TProvider.GetShape());
        private static XmlConverter<T>? s_value;
    }
}
