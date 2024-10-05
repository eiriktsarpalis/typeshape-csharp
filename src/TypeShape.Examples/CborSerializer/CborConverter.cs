using System.Formats.Cbor;

namespace TypeShape.Examples.CborSerializer;

/// <summary>
/// Defines a strongly typed CBOR to .NET converter.
/// </summary>
public abstract class CborConverter
{
    internal CborConverter() { }

    /// <summary>
    /// The type being targeted by the current converter.
    /// </summary>
    public abstract Type Type { get; }
}

/// <summary>
/// Defines a strongly typed CBOR to .NET converter.
/// </summary>
public abstract class CborConverter<T> : CborConverter
{
    /// <inheritdoc/>
    public sealed override Type Type => typeof(T);

    /// <summary>
    /// Writes a value of type <typeparamref name="T"/> to the provided <see cref="CborWriter"/>.
    /// </summary>
    public abstract void Write(CborWriter writer, T? value);

    /// <summary>
    /// Reads a value of type <typeparamref name="T"/> from the provided <see cref="CborReader"/>.
    /// </summary>
    public abstract T? Read(CborReader reader);
}
