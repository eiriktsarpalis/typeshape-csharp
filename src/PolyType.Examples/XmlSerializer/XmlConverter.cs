using System.Xml;

namespace PolyType.Examples.XmlSerializer;

/// <summary>
/// Defines a strongly typed XML to .NET converter.
/// </summary>
public abstract class XmlConverter
{
    internal XmlConverter() { }

    /// <summary>
    /// The type being targeted by the current converter.
    /// </summary>
    public abstract Type Type { get; }
}

/// <summary>
/// Defines a strongly typed XML to .NET converter.
/// </summary>
public abstract class XmlConverter<T> : XmlConverter
{
    /// <inheritdoc/>
    public sealed override Type Type => typeof(T);

    /// <summary>
    /// Writes a value of type <typeparamref name="T"/> to the provided <see cref="XmlWriter"/>.
    /// </summary>
    public abstract void Write(XmlWriter writer, string localName, T? value);

    /// <summary>
    /// Reads a value of type <typeparamref name="T"/> from the provided <see cref="XmlReader"/>.
    /// </summary>
    public abstract T? Read(XmlReader reader);
}
