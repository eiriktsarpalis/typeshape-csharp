using System.Formats.Cbor;
using PolyType.Abstractions;

namespace PolyType.Examples.CborSerializer.Converters;

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

internal sealed class CborObjectConverterWithDefaultCtor<T>(Func<T> defaultConstructor, CborPropertyConverter<T>[] properties) : CborObjectConverter<T>(properties)
{
    private readonly Dictionary<string, CborPropertyConverter<T>> _propertiesToRead = properties.Where(prop => prop.HasSetter).ToDictionary(prop => prop.Name);

    public sealed override T? Read(CborReader reader)
    {
        if (default(T) is null && reader.PeekState() is CborReaderState.Null)
        {
            reader.ReadNull();
            return default;
        }

        reader.ReadStartMap();
        T result = defaultConstructor();
        Dictionary<string, CborPropertyConverter<T>> propertiesToRead = _propertiesToRead;

        while (reader.PeekState() != CborReaderState.EndMap)
        {
            string key = reader.ReadTextString();

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

internal sealed class CborObjectConverterWithParameterizedCtor<TDeclaringType, TArgumentState>(
    Func<TArgumentState> createArgumentState,
    Constructor<TArgumentState, TDeclaringType> createObject,
    CborPropertyConverter<TArgumentState>[] constructorParameters,
    CborPropertyConverter<TDeclaringType>[] properties) : CborObjectConverter<TDeclaringType>(properties)
{
    // Use case-insensitive matching for constructor parameters but case-sensitive matching for property setters.
    private readonly Dictionary<string, CborPropertyConverter<TArgumentState>> _constructorParameters = constructorParameters
        .Where(prop => prop.IsConstructorParameter)
        .ToDictionary(prop => prop.Name, StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, CborPropertyConverter<TArgumentState>> _propertySetters = constructorParameters
        .Where(prop => !prop.IsConstructorParameter)
        .ToDictionary(prop => prop.Name, StringComparer.Ordinal);

    public override TDeclaringType? Read(CborReader reader)
    {
        if (default(TDeclaringType) is null && reader.PeekState() is CborReaderState.Null)
        {
            reader.ReadNull();
            return default;
        }

        reader.ReadStartMap();
        TArgumentState argumentState = createArgumentState();
        Dictionary<string, CborPropertyConverter<TArgumentState>> ctorParams = _constructorParameters;
        Dictionary<string, CborPropertyConverter<TArgumentState>> propertySetters = _propertySetters;

        while (reader.PeekState() != CborReaderState.EndMap)
        {
            string key = reader.ReadTextString();
            if (!ctorParams.TryGetValue(key, out CborPropertyConverter<TArgumentState>? propertyConverter) &&
                !propertySetters.TryGetValue(key, out propertyConverter))
            {
                reader.SkipValue();
                continue;
            }

            propertyConverter.Read(reader, ref argumentState);
        }

        reader.ReadEndMap();
        return createObject(ref argumentState);
    }
}