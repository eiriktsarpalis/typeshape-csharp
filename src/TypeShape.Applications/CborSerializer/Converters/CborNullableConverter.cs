using System.Formats.Cbor;

namespace TypeShape.Applications.CborSerializer.Converters;

internal sealed class CborNullableConverter<T> : CborConverter<T?>
    where T : struct
{
    private readonly CborConverter<T> _elementConverter;

    public CborNullableConverter(CborConverter<T> elementConverter)
        => _elementConverter = elementConverter;

    public override T? Read(CborReader reader)
    {
        if (reader.PeekState() is CborReaderState.Null)
        {
            reader.ReadNull();
            return null;
        }

        return _elementConverter.Read(reader);
    }

    public override void Write(CborWriter writer, T? value)
    {
        if (value is null)
        {
            writer.WriteNull();
            return;
        }

        _elementConverter.Write(writer, value.Value);
    }
}
