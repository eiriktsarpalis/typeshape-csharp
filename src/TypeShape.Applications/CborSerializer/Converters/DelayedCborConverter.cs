using System.Diagnostics;
using System.Formats.Cbor;

namespace TypeShape.Applications.CborSerializer.Converters
{
    internal sealed class DelayedCborConverter<T>(ResultHolder<CborConverter<T>> holder) : CborConverter<T>
    {
        public CborConverter<T> Underlying
        {
            get
            {
                Debug.Assert(holder.Value != null);
                return holder.Value;
            }
        }

        public override T? Read(CborReader reader)
            => Underlying.Read(reader);

        public override void Write(CborWriter writer, T? value)
            => Underlying.Write(writer, value);
    }
}
