using System.Xml;
using TypeShape.Abstractions;

namespace TypeShape.Applications.XmlSerializer.Converters;

internal class XmlObjectConverter<T>(XmlPropertyConverter<T>[] properties) : XmlConverter<T>
{
    private readonly XmlPropertyConverter<T>[] _propertiesToWrite = properties.Where(prop => prop.HasGetter).ToArray();

    public override T? Read(XmlReader reader)
        => throw new NotSupportedException($"Deserialization for type {typeof(T)} is not supported.");

    public sealed override void Write(XmlWriter writer, string localName, T? value)
    {
        if (value is null)
        {
            writer.WriteNullElement(localName);
            return;
        }

        writer.WriteStartElement(localName);
        if (value is not null)
        {
            foreach (XmlPropertyConverter<T> property in _propertiesToWrite)
            {
                property.Write(writer, ref value);
            }
        }
        writer.WriteEndElement();
    }
}

internal sealed class XmlObjectConverterWithDefaultCtor<T>(Func<T> defaultConstructor, XmlPropertyConverter<T>[] properties) : XmlObjectConverter<T>(properties)
{
    private readonly Dictionary<string, XmlPropertyConverter<T>> _propertiesToRead = properties.Where(prop => prop.HasSetter).ToDictionary(prop => prop.Name);

    public sealed override T? Read(XmlReader reader)
    {
        if (default(T) is null && reader.TryReadNullElement())
        {
            return default;
        }

        bool isEmptyElement = reader.IsEmptyElement;
        reader.ReadStartElement();
        T result = defaultConstructor();

        if (isEmptyElement)
        {
            return result;
        }

        Dictionary<string, XmlPropertyConverter<T>> propertiesToRead = _propertiesToRead;

        while (reader.NodeType != XmlNodeType.EndElement)
        {
            if (reader.NodeType != XmlNodeType.Element)
            {
                continue;
            }

            string key = reader.Name;

            if (!propertiesToRead.TryGetValue(key, out XmlPropertyConverter<T>? propConverter))
            {
                reader.Skip();
                continue;
            }

            propConverter.Read(reader, ref result);
        }

        reader.ReadEndElement();
        return result;
    }
}

internal sealed class XmlObjectConverterWithParameterizedCtor<TDeclaringType, TArgumentState>(
    Func<TArgumentState> createArgumentState,
    Constructor<TArgumentState, TDeclaringType> createObject,
    XmlPropertyConverter<TArgumentState>[] constructorParameters,
    XmlPropertyConverter<TDeclaringType>[] properties) : XmlObjectConverter<TDeclaringType>(properties)
{
    // Use case-insensitive matching for constructor parameters but case-sensitive matching for property setters.
    private readonly Dictionary<string, XmlPropertyConverter<TArgumentState>> _constructorParameters = constructorParameters
        .Where(p => p.IsConstructorParameter)
        .ToDictionary(param => param.Name, StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, XmlPropertyConverter<TArgumentState>> _propertiesToRead = constructorParameters
        .Where(p => !p.IsConstructorParameter)
        .ToDictionary(param => param.Name, StringComparer.Ordinal);

    public override TDeclaringType? Read(XmlReader reader)
    {
        if (default(TDeclaringType) is null && reader.TryReadNullElement())
        {
            return default;
        }

        bool isEmptyElement = reader.IsEmptyElement;
        reader.ReadStartElement();
        TArgumentState argumentState = createArgumentState();

        if (!isEmptyElement)
        {
            Dictionary<string, XmlPropertyConverter<TArgumentState>> ctorParams = _constructorParameters;
            Dictionary<string, XmlPropertyConverter<TArgumentState>> propertiesToRead = _propertiesToRead;

            while (reader.NodeType != XmlNodeType.EndElement)
            {
                if (reader.NodeType != XmlNodeType.Element)
                {
                    continue;
                }

                string key = reader.Name;
                if (!ctorParams.TryGetValue(key, out XmlPropertyConverter<TArgumentState>? propertyConverter) &&
                    !propertiesToRead.TryGetValue(key, out propertyConverter))
                {
                    reader.Skip();
                    continue;
                }

                propertyConverter.Read(reader, ref argumentState);
            }
        }

        reader.ReadEndElement();
        return createObject(ref argumentState);
    }
}