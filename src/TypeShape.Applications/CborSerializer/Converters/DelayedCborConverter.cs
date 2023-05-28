using System.Diagnostics;
using System.Formats.Cbor;

namespace TypeShape.Applications.CborSerializer.Converters
{
    internal sealed class DelayedCborConverter<T> : CborConverter<T>
    {
        private readonly ResultHolder<CborConverter<T>> _holder;

        public DelayedCborConverter(ResultHolder<CborConverter<T>> holder)
            => _holder = holder;

        public CborConverter<T> Underlying
        {
            get
            {
                Debug.Assert(_holder.Value != null);
                return _holder.Value;
            }
        }

        public override T? Read(CborReader reader)
            => Underlying.Read(reader);

        public override void Write(CborWriter writer, T? value)
            => Underlying.Write(writer, value);
    }
}
