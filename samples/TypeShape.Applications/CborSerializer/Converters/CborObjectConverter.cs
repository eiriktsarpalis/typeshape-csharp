using System.Formats.Cbor;

namespace TypeShape.Applications.CborSerializer.Converters;

internal class CborObjectConverter<T>(CborPropertyConverter<T>[] properties) : CborConverter<T>
{
    private readonly CborPropertyConverter<T>[] _propertiesToWrite = properties.Where(prop => prop.HasGetter).ToArray();

    public override T? Read(CborReader reader)
        => throw new NotSupportedException($"Deserialization for type {typeof(T)} is not supported.");

    public sealed override void Write(CborWriter writer, T? value)
    {
        if (value is null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteStartMap(_propertiesToWrite.Length);
        foreach (CborPropertyConverter<T> property in _propertiesToWrite)
        {
            writer.WriteTextString(property.Name);
            property.Write(writer, ref value);
        }

        writer.WriteEndMap();
    }
}

internal abstract class CborObjectConverterWithCtor<T>(CborPropertyConverter<T>[] properties) : CborObjectConverter<T>(properties)
{
    private readonly Dictionary<string, CborPropertyConverter<T>> _propertiesToRead = properties.Where(prop => prop.HasSetter).ToDictionary(prop => prop.Name);

    protected abstract T ReadConstructorParametersAndCreateObject(CborReader reader, out string? pendingPropertyName);

    public sealed override T? Read(CborReader reader)
    {
        if (default(T) is null && reader.PeekState() is CborReaderState.Null)
        {
            reader.ReadNull();
            return default;
        }

        reader.ReadStartMap();
        T result = ReadConstructorParametersAndCreateObject(reader, out string? pendingPropertyName);
        Dictionary<string, CborPropertyConverter<T>> propertiesToRead = _propertiesToRead;

        while (reader.PeekState() != CborReaderState.EndMap)
        {
            string key = pendingPropertyName ?? reader.ReadTextString();
            pendingPropertyName = null;

            if (!propertiesToRead.TryGetValue(key, out CborPropertyConverter<T>? propConverter))
            {
                reader.SkipValue();
                continue;
            }

            propConverter.Read(reader, ref result);
        }

        reader.ReadEndMap();
        return result;
    }
}

internal sealed class CborObjectConverterWithDefaultCtor<T>(
    Func<T> defaultConstructor, 
    CborPropertyConverter<T>[] properties) : CborObjectConverterWithCtor<T>(properties)
{
    protected override T ReadConstructorParametersAndCreateObject(CborReader reader, out string? pendingPropertyName)
    {
        pendingPropertyName = null;
        return defaultConstructor();
    }
}

internal sealed class CborObjectConverterWithParameterizedCtor<TDeclaringType, TArgumentState>(
    Func<TArgumentState> createArgumentState,
    Func<TArgumentState, TDeclaringType> createObject,
    CborPropertyConverter<TArgumentState>[] constructorParameters,
    CborPropertyConverter<TDeclaringType>[] properties) : CborObjectConverterWithCtor<TDeclaringType>(properties)
{
    private readonly Dictionary<string, CborPropertyConverter<TArgumentState>> _constructorParameters = constructorParameters.ToDictionary(param => param.Name, StringComparer.OrdinalIgnoreCase);

    protected override TDeclaringType ReadConstructorParametersAndCreateObject(CborReader reader, out string? pendingPropertyName)
    {
        Dictionary<string, CborPropertyConverter<TArgumentState>> ctorParams = _constructorParameters;
        TArgumentState argumentState = createArgumentState();
        pendingPropertyName = null;

        while (reader.PeekState() != CborReaderState.EndMap)
        {
            string propertyName = reader.ReadTextString();
            if (!ctorParams.TryGetValue(propertyName, out CborPropertyConverter<TArgumentState>? propertyConverter))
            {
                // stop reading constructor arguments on the first unrecognized parameter
                pendingPropertyName = propertyName;
                break; 
            }

            propertyConverter.Read(reader, ref argumentState);
        }

        return createObject(argumentState);
    }
}