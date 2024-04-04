using System.Formats.Cbor;

namespace TypeShape.Applications.CborSerializer.Converters
{
    internal sealed class DelayedCborConverter<T>(ResultBox<CborConverter<T>> self) : CborConverter<T>
    {
        public override T? Read(CborReader reader)
            => self.Result.Read(reader);

        public override void Write(CborWriter writer, T? value)
            => self.Result.Write(writer, value);
    }
}
