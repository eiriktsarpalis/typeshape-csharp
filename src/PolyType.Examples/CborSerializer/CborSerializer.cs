using System.Diagnostics;
using System.Formats.Cbor;
using PolyType.Abstractions;

namespace PolyType.Examples.CborSerializer;

/// <summary>
/// Provides a CBOR serialization implementation built on top of PolyType.
/// </summary>
public static partial class CborSerializer
{
    /// <summary>
    /// Builds an <see cref="CborConverter{T}"/> instance from the specified shape.
    /// </summary>
    /// <typeparam name="T">The type for which to build the converter.</typeparam>
    /// <param name="shape">The shape instance guiding converter construction.</param>
    /// <returns>An <see cref="CborConverter{T}"/> instance.</returns>
    public static CborConverter<T> CreateConverter<T>(ITypeShape<T> shape) =>
        new Builder().BuildConverter(shape);

    /// <summary>
    /// Builds an <see cref="CborConverter{T}"/> instance from the specified shape provider.
    /// </summary>
    /// <typeparam name="T">The type for which to build the converter.</typeparam>
    /// <param name="shapeProvider">The shape provider converter construction.</param>
    /// <returns>An <see cref="CborConverter{T}"/> instance.</returns>
    public static CborConverter<T> CreateConverter<T>(ITypeShapeProvider shapeProvider) =>
        CreateConverter(shapeProvider.Resolve<T>());

    /// <summary>
    /// Serializes a value to a CBOR encoding using the provided converter.
    /// </summary>
    /// <typeparam name="T">The type of the value to serialize.</typeparam>
    /// <param name="converter">The converter used to serialize the value.</param>
    /// <param name="value">The value to be serialized.</param>
    /// <returns>An CBOR encoded buffer containing the serialized value.</returns>
    public static byte[] Encode<T>(this CborConverter<T> converter, T? value)
    {
        var writer = new CborWriter(CborConformanceMode.Lax, convertIndefiniteLengthEncodings: false, allowMultipleRootLevelValues: false);
        converter.Write(writer, value);
        return writer.Encode();
    }

    /// <summary>
    /// Deserializes a value from a CBOR encoding using the provided converter.
    /// </summary>
    /// <typeparam name="T">The type of the value to deserialize.</typeparam>
    /// <param name="converter">The converter used to deserialize the value.</param>
    /// <param name="encoding">The CBOR encoding to be deserialized.</param>
    /// <returns>The deserialized value.</returns>
    public static T? Decode<T>(this CborConverter<T> converter, byte[] encoding)
    {
        var reader = new CborReader(encoding, CborConformanceMode.Lax, allowMultipleRootLevelValues: false);
        T? result = converter.Read(reader);
        Debug.Assert(reader.CurrentDepth == 0);
        return result;
    }

    /// <summary>
    /// Serializes a value to a CBOR encoding using the provided converter.
    /// </summary>
    /// <typeparam name="T">The type of the value to serialize.</typeparam>
    /// <param name="converter">The converter used to serialize the value.</param>
    /// <param name="value">The value to be serialized.</param>
    /// <returns>A hex-encoded CBOR string containing the serialized value.</returns>
    public static string EncodeToHex<T>(this CborConverter<T> converter, T? value)
    {
        byte[] encoding = converter.Encode(value);
        return Convert.ToHexString(encoding);
    }

    /// <summary>
    /// Deserializes a value from a CBOR encoding using the provided converter.
    /// </summary>
    /// <typeparam name="T">The type of the value to deserialize.</typeparam>
    /// <param name="converter">The converter used to deserialize the value.</param>
    /// <param name="hexEncoding">The CBOR encoded hex string to be deserialized.</param>
    /// <returns>The deserialized value.</returns>
    public static T? DecodeFromHex<T>(this CborConverter<T> converter, string hexEncoding)
    {
        byte[] encoding = Convert.FromHexString(hexEncoding);
        return converter.Decode(encoding);
    }

    /// <summary>
    /// Serializes a value to a CBOR encoding using its <see cref="IShapeable{T}"/> implementation.
    /// </summary>
    /// <typeparam name="T">The type of the value to serialize.</typeparam>
    /// <param name="value">The value to be serialized.</param>
    /// <returns>A hex-encoded CBOR string containing the serialized value.</returns>
    public static byte[] Encode<T>(T? value) where T : IShapeable<T>
        => CborSerializerCache<T, T>.Value.Encode(value);

    /// <summary>
    /// Deserializes a value from a CBOR using its <see cref="IShapeable{T}"/> implementation.
    /// </summary>
    /// <typeparam name="T">The type of the value to deserialize.</typeparam>
    /// <param name="encoding">The CBOR encoding to be deserialized.</param>
    /// <returns>The deserialized value.</returns>
    public static T? Decode<T>(byte[] encoding) where T : IShapeable<T>
        => CborSerializerCache<T, T>.Value.Decode(encoding);

    /// <summary>
    /// Serializes a value to a CBOR encoding using its <see cref="IShapeable{T}"/> implementation.
    /// </summary>
    /// <typeparam name="T">The type of the value to serialize.</typeparam>
    /// <param name="value">The value to be serialized.</param>
    /// <returns>A hex-encoded CBOR string containing the serialized value.</returns>
    public static string EncodeToHex<T>(T? value) where T : IShapeable<T>
        => CborSerializerCache<T, T>.Value.EncodeToHex(value);

    /// <summary>
    /// Deserializes a value from a CBOR encoding using its <see cref="IShapeable{T}"/> implementation.
    /// </summary>
    /// <typeparam name="T">The type of the value to deserialize.</typeparam>
    /// <param name="hexEncoding">The CBOR encoded hex string to be deserialized.</param>
    /// <returns>The deserialized value.</returns>
    public static T? DecodeFromHex<T>(string hexEncoding) where T : IShapeable<T>
        => CborSerializerCache<T, T>.Value.DecodeFromHex(hexEncoding);

    /// <summary>
    /// Serializes a value to a CBOR encoding using an externally provided <see cref="IShapeable{T}"/> implementation.
    /// </summary>
    /// <typeparam name="T">The type of the value to serialize.</typeparam>
    /// <typeparam name="TProvider">The type providing an <see cref="IShapeable{T}"/> implementation.</typeparam>
    /// <param name="value">The value to be serialized.</param>
    /// <returns>A hex-encoded CBOR string containing the serialized value.</returns>
    public static byte[] Encode<T, TProvider>(T? value) where TProvider : IShapeable<T>
        => CborSerializerCache<T, TProvider>.Value.Encode(value);

    /// <summary>
    /// Deserializes a value from a CBOR using an externally provided <see cref="IShapeable{T}"/> implementation.
    /// </summary>
    /// <typeparam name="T">The type of the value to deserialize.</typeparam>
    /// <typeparam name="TProvider">The type providing an <see cref="IShapeable{T}"/> implementation.</typeparam>
    /// <param name="encoding">The CBOR encoding to be deserialized.</param>
    /// <returns>The deserialized value.</returns>
    public static T? Decode<T, TProvider>(byte[] encoding) where TProvider : IShapeable<T>
        => CborSerializerCache<T, TProvider>.Value.Decode(encoding);

    /// <summary>
    /// Serializes a value to a CBOR encoding using an externally provided <see cref="IShapeable{T}"/> implementation.
    /// </summary>
    /// <typeparam name="T">The type of the value to serialize.</typeparam>
    /// <typeparam name="TProvider">The type providing an <see cref="IShapeable{T}"/> implementation.</typeparam>
    /// <param name="value">The value to be serialized.</param>
    /// <returns>A hex-encoded CBOR string containing the serialized value.</returns>
    public static string EncodeToHex<T, TProvider>(T? value) where TProvider : IShapeable<T>
        => CborSerializerCache<T, TProvider>.Value.EncodeToHex(value);

    /// <summary>
    /// Deserializes a value from a CBOR encoding using an externally provided <see cref="IShapeable{T}"/> implementation.
    /// </summary>
    /// <typeparam name="T">The type of the value to deserialize.</typeparam>
    /// <typeparam name="TProvider">The type providing an <see cref="IShapeable{T}"/> implementation.</typeparam>
    /// <param name="hexEncoding">The CBOR encoded hex string to be deserialized.</param>
    /// <returns>The deserialized value.</returns>
    public static T? DecodeFromHex<T, TProvider>(string hexEncoding) where TProvider : IShapeable<T>
        => CborSerializerCache<T, TProvider>.Value.DecodeFromHex(hexEncoding);

    private static class CborSerializerCache<T, TProvider> where TProvider : IShapeable<T>
    {
        public static CborConverter<T> Value => s_value ??= CreateConverter(TProvider.GetShape());
        private static CborConverter<T>? s_value;
    }
}
