using System.Formats.Cbor;

namespace TypeShape.Examples.CborSerializer;

public abstract class CborConverter
{
    internal CborConverter() { }
    public abstract Type Type { get; }
}

public abstract class CborConverter<T> : CborConverter
{
    public sealed override Type Type => typeof(T);
    public abstract void Write(CborWriter writer, T? value);
    public abstract T? Read(CborReader reader);
}
