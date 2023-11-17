using System.Xml;

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

internal abstract class XmlObjectConverterWithCtor<T>(XmlPropertyConverter<T>[] properties) : XmlObjectConverter<T>(properties)
{
    private readonly Dictionary<string, XmlPropertyConverter<T>> _propertiesToRead = properties.Where(prop => prop.HasSetter).ToDictionary(prop => prop.Name);

    protected abstract T ReadConstructorParametersAndCreateObject(XmlReader reader, bool isEmptyElement);

    public sealed override T? Read(XmlReader reader)
    {
        if (default(T) is null && reader.TryReadNullElement())
        {
            return default;
        }

        bool isEmptyElement = reader.IsEmptyElement;
        reader.ReadStartElement();
        T result = ReadConstructorParametersAndCreateObject(reader, isEmptyElement);

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

internal sealed class XmlObjectConverterWithDefaultCtor<T>(
    Func<T> defaultConstructor,
    XmlPropertyConverter<T>[] properties) : XmlObjectConverterWithCtor<T>(properties)
{
    protected override T ReadConstructorParametersAndCreateObject(XmlReader reader, bool _)
        => defaultConstructor();
}

internal sealed class XmlObjectConverterWithParameterizedCtor<TDeclaringType, TArgumentState>(
    Func<TArgumentState> createArgumentState,
    Constructor<TArgumentState, TDeclaringType> createObject,
    XmlPropertyConverter<TArgumentState>[] constructorParameters,
    XmlPropertyConverter<TDeclaringType>[] properties) : XmlObjectConverterWithCtor<TDeclaringType>(properties)
{
    private readonly Dictionary<string, XmlPropertyConverter<TArgumentState>> _constructorParameters = constructorParameters.ToDictionary(param => param.Name, StringComparer.OrdinalIgnoreCase);

    protected override TDeclaringType ReadConstructorParametersAndCreateObject(XmlReader reader, bool isEmptyElement)
    {
        Dictionary<string, XmlPropertyConverter<TArgumentState>> ctorParams = _constructorParameters;
        TArgumentState argumentState = createArgumentState();

        if (!isEmptyElement)
        {
            while (reader.NodeType != XmlNodeType.EndElement)
            {
                if (reader.NodeType != XmlNodeType.Element)
                {
                    continue;
                }

                if (!ctorParams.TryGetValue(reader.Name, out XmlPropertyConverter<TArgumentState>? propertyConverter))
                {
                    // stop reading constructor arguments on the first unrecognized parameter
                    break;
                }

                propertyConverter.Read(reader, ref argumentState);
            }
        }

        return createObject(argumentState);
    }
}