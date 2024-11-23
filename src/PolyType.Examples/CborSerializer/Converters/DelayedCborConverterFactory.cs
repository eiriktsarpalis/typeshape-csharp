using PolyType.Abstractions;
using PolyType.Utilities;
using System.Formats.Cbor;

namespace PolyType.Examples.CborSerializer.Converters;

internal sealed class DelayedCborConverterFactory : IDelayedValueFactory
{
    public DelayedValue Create<T>(ITypeShape<T> _) => new DelayedValue<CborConverter<T>>(self => new DelayedCborConverter<T>(self));

    private sealed class DelayedCborConverter<T>(DelayedValue<CborConverter<T>> self) : CborConverter<T>
    {
        public override T? Read(CborReader reader)
            => self.Result.Read(reader);

        public override void Write(CborWriter writer, T? value)
            => self.Result.Write(writer, value);
    }
}
