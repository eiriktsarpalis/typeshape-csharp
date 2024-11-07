using System.Formats.Cbor;
using PolyType.Examples.Utilities;

namespace PolyType.Examples.CborSerializer.Converters;

internal sealed class DelayedCborConverter<T>(ResultBox<CborConverter<T>> self) : CborConverter<T>
{
    public override T? Read(CborReader reader)
        => self.Result.Read(reader);

    public override void Write(CborWriter writer, T? value)
        => self.Result.Write(writer, value);
}
