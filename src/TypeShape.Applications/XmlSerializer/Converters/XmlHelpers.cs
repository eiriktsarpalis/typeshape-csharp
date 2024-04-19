using System.Runtime.CompilerServices;
using System.Xml;

namespace TypeShape.Applications.XmlSerializer.Converters;

internal static class XmlHelpers
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void EnsureRead(this XmlReader reader)
    {
        if (!reader.Read())
        {
            Throw();
            static void Throw() => throw new InvalidOperationException("Unexpected end of XML stream.");
        }
    }

    public static void WriteNullElement(this XmlWriter writer, string localName)
    {
        writer.WriteStartElement(localName);
        writer.WriteAttributeString("nil", "true");
        writer.WriteEndElement();
    }

    public static bool IsNullElement(this XmlReader reader)
    {
        string? attribute = reader.GetAttribute("nil");
        return attribute != null && XmlConvert.ToBoolean(attribute);
    }

    public static bool TryReadNullElement(this XmlReader reader)
    {
        if (reader.IsNullElement())
        {
            reader.Read();
            return true;
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static XmlNodeType ReadNextNode(this XmlReader reader)
    {
        reader.Read();
        return reader.NodeType;
    }
}
