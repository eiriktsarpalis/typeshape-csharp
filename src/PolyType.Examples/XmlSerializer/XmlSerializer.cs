using PolyType.Abstractions;
using PolyType.Examples.XmlSerializer.Converters;
using PolyType.Utilities;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Xml;

namespace PolyType.Examples.XmlSerializer;

/// <summary>
/// Provides an XML serialization implementation built on top of PolyType.
/// </summary>
public static partial class XmlSerializer
{
    private static readonly MultiProviderTypeCache s_converterCaches = new()
    {
        DelayedValueFactory = new DelayedXmlConverterFactory(),
        ValueBuilderFactory = ctx => new Builder(ctx),
    };

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

    /// <summary>
    /// Builds an <see cref="XmlConverter{T}"/> instance from the specified shape.
    /// </summary>
    /// <typeparam name="T">The type for which to build the converter.</typeparam>
    /// <param name="shape">The shape instance guiding converter construction.</param>
    /// <returns>An <see cref="XmlConverter{T}"/> instance.</returns>
    public static XmlConverter<T> CreateConverter<T>(ITypeShape<T> shape) =>
        (XmlConverter<T>)s_converterCaches.GetOrAdd(shape)!;

    /// <summary>
    /// Builds an <see cref="XmlConverter{T}"/> instance from the specified shape provider.
    /// </summary>
    /// <typeparam name="T">The type for which to build the converter.</typeparam>
    /// <param name="shapeProvider">The shape provider guiding converter construction.</param>
    /// <returns>An <see cref="XmlConverter{T}"/> instance.</returns>
    public static XmlConverter<T> CreateConverter<T>(ITypeShapeProvider shapeProvider) =>
        (XmlConverter<T>)s_converterCaches.GetOrAdd(typeof(T), shapeProvider)!;

    /// <summary>
    /// Serializes a value to an XML string using the provided converter.
    /// </summary>
    /// <typeparam name="T">The type of the value to serialize.</typeparam>
    /// <param name="converter">The converter used to serialize the value.</param>
    /// <param name="value">The value to be serialized.</param>
    /// <param name="settings">The setting object guiding XML formatting.</param>
    /// <returns>An XML encoded string containing the serialized value.</returns>
    public static string Serialize<T>(this XmlConverter<T> converter, T? value, XmlWriterSettings? settings = null)
    {
        using var sw = new StringWriter();
        using var writer = XmlWriter.Create(sw, settings ?? s_writerSettings);
        converter.Write(writer, localName: "value", value);
        writer.Flush();
        return sw.ToString();
    }

    /// <summary>
    /// Deserializes a value from an XML string using the provided converter.
    /// </summary>
    /// <typeparam name="T">The type of the value to deserialize.</typeparam>
    /// <param name="converter">The converter used to deserialize the value.</param>
    /// <param name="xml">The XML encoding to be deserialized.</param>
    /// <param name="settings">The setting object guiding XML reading.</param>
    /// <returns>The deserialized value.</returns>
    public static T? Deserialize<T>(this XmlConverter<T> converter, [StringSyntax(StringSyntaxAttribute.Json)] string xml, XmlReaderSettings? settings = null)
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

#if NET
    /// <summary>
    /// Serializes a value to an XML string using its <see cref="IShapeable{T}"/> implementation.
    /// </summary>
    /// <typeparam name="T">The type of the value to serialize.</typeparam>
    /// <param name="value">The value to be serialized.</param>
    /// <param name="settings">The setting object guiding XML formatting.</param>
    /// <returns>An XML encoded string containing the serialized value.</returns>
    public static string Serialize<T>(T? value, XmlWriterSettings? settings = null) where T : IShapeable<T>
        => XmlSerializerCache<T, T>.Value.Serialize(value, settings);

    /// <summary>
    /// Deserializes a value from an XML string using its <see cref="IShapeable{T}"/> implementation.
    /// </summary>
    /// <typeparam name="T">The type of the value to deserialize.</typeparam>
    /// <param name="xml">The XML encoding to be deserialized.</param>
    /// <param name="settings">The setting object guiding XML reading.</param>
    /// <returns>The deserialized value.</returns>
    public static T? Deserialize<T>(string xml, XmlReaderSettings? settings = null) where T : IShapeable<T>
        => XmlSerializerCache<T, T>.Value.Deserialize(xml, settings);

    /// <summary>
    /// Serializes a value to an XML string using an externally provided <see cref="IShapeable{T}"/> implementation.
    /// </summary>
    /// <typeparam name="T">The type of the value to serialize.</typeparam>
    /// <typeparam name="TProvider">The type providing an <see cref="IShapeable{T}"/> implementation.</typeparam>
    /// <param name="value">The value to be serialized.</param>
    /// <param name="settings">The setting object guiding XML formatting.</param>
    /// <returns>An XML encoded string containing the serialized value.</returns>
    public static string Serialize<T, TProvider>(T? value, XmlWriterSettings? settings = null) where TProvider : IShapeable<T>
        => XmlSerializerCache<T, TProvider>.Value.Serialize(value, settings);

    /// <summary>
    /// Deserializes a value from an XML string using an externally provided <see cref="IShapeable{T}"/> implementation.
    /// </summary>
    /// <typeparam name="T">The type of the value to deserialize.</typeparam>
    /// <typeparam name="TProvider">The type providing an <see cref="IShapeable{T}"/> implementation.</typeparam>
    /// <param name="xml">The XML encoding to be deserialized.</param>
    /// <param name="settings">The setting object guiding XML reading.</param>
    /// <returns>The deserialized value.</returns>
    public static T? Deserialize<T, TProvider>(string xml, XmlReaderSettings? settings = null) where TProvider : IShapeable<T>
        => XmlSerializerCache<T, TProvider>.Value.Deserialize(xml, settings);

    private static class XmlSerializerCache<T, TProvider> where TProvider : IShapeable<T>
    {
        public static XmlConverter<T> Value => s_value ??= CreateConverter(TProvider.GetShape());
        private static XmlConverter<T>? s_value;
    }
#endif
}
