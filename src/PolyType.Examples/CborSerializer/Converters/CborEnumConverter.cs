using System.Formats.Cbor;

namespace PolyType.Examples.CborSerializer.Converters;

internal sealed class CborEnumConverter<TEnum> : CborConverter<TEnum>
    where TEnum : struct, Enum
{
    public override TEnum Read(CborReader reader)
#if NET
        => Enum.Parse<TEnum>(reader.ReadTextString());
#else
        => (TEnum)Enum.Parse(typeof(TEnum), reader.ReadTextString());
#endif

    public override void Write(CborWriter writer, TEnum value)
        => writer.WriteTextString(value.ToString());
}
