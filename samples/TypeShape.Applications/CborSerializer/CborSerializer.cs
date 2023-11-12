using System.Formats.Cbor;

namespace TypeShape.Applications.CborSerializer;

public static partial class CborSerializer
{
    public static CborConverter<T> CreateConverter<T>(ITypeShape<T> shape)
    {
        var visitor = new Visitor();
        return (CborConverter<T>)shape.Accept(visitor, null)!;
    }

    public static byte[] Encode<T>(this CborConverter<T> converter, T? value)
    {
        var writer = new CborWriter(CborConformanceMode.Lax, convertIndefiniteLengthEncodings: false, allowMultipleRootLevelValues: false);
        converter.Write(writer, value);
        return writer.Encode();
    }

    public static T? Decode<T>(this CborConverter<T> converter, byte[] encoding)
    {
        var reader = new CborReader(encoding, CborConformanceMode.Lax, allowMultipleRootLevelValues: false);
        return converter.Read(reader);
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
}
