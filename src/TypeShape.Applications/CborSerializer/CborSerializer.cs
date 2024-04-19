using System.Diagnostics;
using System.Formats.Cbor;
using TypeShape.Abstractions;

namespace TypeShape.Applications.CborSerializer;

public static partial class CborSerializer
{
    public static CborConverter<T> CreateConverter<T>(ITypeShape<T> shape) =>
        new Builder().BuildConverter(shape);

    public static CborConverter<T> CreateConverter<T>(ITypeShapeProvider provider) =>
        CreateConverter(provider.Resolve<T>());

    public static byte[] Encode<T>(this CborConverter<T> converter, T? value)
    {
        var writer = new CborWriter(CborConformanceMode.Lax, convertIndefiniteLengthEncodings: false, allowMultipleRootLevelValues: false);
        converter.Write(writer, value);
        return writer.Encode();
    }

    public static T? Decode<T>(this CborConverter<T> converter, byte[] encoding)
    {
        var reader = new CborReader(encoding, CborConformanceMode.Lax, allowMultipleRootLevelValues: false);
        T? result = converter.Read(reader);
        Debug.Assert(reader.CurrentDepth == 0);
        return result;
    }

    public static string EncodeToHex<T>(this CborConverter<T> converter, T? value)
    {
        byte[] encoding = converter.Encode(value);
        return Convert.ToHexString(encoding);
    }

    public static T? DecodeFromHex<T>(this CborConverter<T> converter, string hexEncoding)
    {
        byte[] encoding = Convert.FromHexString(hexEncoding);
        return converter.Decode(encoding);
    }

    public static byte[] Encode<T>(T? value) where T : ITypeShapeProvider<T>
        => CborSerializerCache<T, T>.Value.Encode(value);

    public static T? Decode<T>(byte[] encoding) where T : ITypeShapeProvider<T>
        => CborSerializerCache<T, T>.Value.Decode(encoding);

    public static string EncodeToHex<T>(T? value) where T : ITypeShapeProvider<T>
        => CborSerializerCache<T, T>.Value.EncodeToHex(value);

    public static T? DecodeFromHex<T>(string hexEncoding) where T : ITypeShapeProvider<T>
        => CborSerializerCache<T, T>.Value.DecodeFromHex(hexEncoding);

    public static byte[] Encode<T, TProvider>(T? value) where TProvider : ITypeShapeProvider<T>
        => CborSerializerCache<T, TProvider>.Value.Encode(value);

    public static T? Decode<T, TProvider>(byte[] encoding) where TProvider : ITypeShapeProvider<T>
        => CborSerializerCache<T, TProvider>.Value.Decode(encoding);

    public static string EncodeToHex<T, TProvider>(T? value) where TProvider : ITypeShapeProvider<T>
        => CborSerializerCache<T, TProvider>.Value.EncodeToHex(value);

    public static T? DecodeFromHex<T, TProvider>(string hexEncoding) where TProvider : ITypeShapeProvider<T>
        => CborSerializerCache<T, TProvider>.Value.DecodeFromHex(hexEncoding);

    private static class CborSerializerCache<T, TProvider> where TProvider : ITypeShapeProvider<T>
    {
        public static CborConverter<T> Value => s_value ??= CreateConverter(TProvider.GetShape());
        private static CborConverter<T>? s_value;
    }
}
