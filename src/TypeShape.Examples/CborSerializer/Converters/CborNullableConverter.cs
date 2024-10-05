using System.Formats.Cbor;

namespace TypeShape.Examples.CborSerializer.Converters;

internal sealed class CborNullableConverter<T>(CborConverter<T> elementConverter) : CborConverter<T?>
    where T : struct
{
    public override T? Read(CborReader reader)
    {
        if (reader.PeekState() is CborReaderState.Null)
        {
            reader.ReadNull();
            return null;
        }

        return elementConverter.Read(reader);
    }

    public override void Write(CborWriter writer, T? value)
    {
        if (value is null)
        {
            writer.WriteNull();
            return;
        }

        elementConverter.Write(writer, value.Value);
    }
}
