using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeShape.Abstractions;

namespace TypeShape.Examples.JsonSerializer.Converters;

/// <summary>Defines a converter for <see cref="BigInteger"/>.</summary>
public sealed class BigIntegerConverter : JsonConverter<BigInteger>
{
    /// <inheritdoc/>
    public sealed override BigInteger Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        Debug.Assert(!reader.HasValueSequence); // https://github.com/dotnet/runtime/issues/84375

        if (reader.TokenType != JsonTokenType.Number)
        {
            reader.GetInt32(); // force a token type exception.
        }

        char[]? rentedBuffer = null;
        int bytesLength = reader.ValueSpan.Length;
        Span<char> destination = bytesLength <= 128
            ? stackalloc char[128]
            : rentedBuffer = ArrayPool<char>.Shared.Rent(bytesLength);

        int charLength = Encoding.UTF8.GetChars(reader.ValueSpan, destination);

        try
        {
            return BigInteger.Parse(destination[..charLength], CultureInfo.InvariantCulture);
        }
        finally
        {
            if (rentedBuffer != null)
            {
                rentedBuffer.AsSpan(0, charLength).Clear();
                ArrayPool<char>.Shared.Return(rentedBuffer);
            }
        }
    }

    /// <inheritdoc/>
    public sealed override void Write(Utf8JsonWriter writer, BigInteger value, JsonSerializerOptions options)
    {
        char[]? rentedBuffer = null;
        int maxLength = (int)Math.Ceiling(value.GetByteCount() * 8.0 / Math.Log10(2));

        Span<char> destination = maxLength <= 128
            ? stackalloc char[128]
            : rentedBuffer = ArrayPool<char>.Shared.Rent(maxLength);

        bool success = value.TryFormat(destination, out int charsWritten, provider: CultureInfo.InvariantCulture);
        Debug.Assert(success);
        writer.WriteRawValue(destination[..charsWritten]);

        if (rentedBuffer != null)
        {
            rentedBuffer.AsSpan(0, charsWritten).Clear();
            ArrayPool<char>.Shared.Return(rentedBuffer);
        }
    }
}

/// <summary>Defines a converter for <see cref="Rune"/>.</summary>
public sealed class RuneConverter : JsonConverter<Rune>
{
    /// <inheritdoc/>
    public override Rune Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return Rune.GetRuneAt(reader.GetString()!, 0);
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, Rune value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}

internal sealed class JsonObjectConverter(ITypeShapeProvider shapeProvider) : JsonConverter<object?>
{
    private static readonly ConcurrentDictionary<Type, JsonConverter> _derivedTypes = new();
    public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType is JsonTokenType.Null ? null : JsonDocument.ParseValue(ref reader).RootElement;
    }

    public override void Write(Utf8JsonWriter writer, object? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        JsonConverter? derivedConverter = GetDerivedConverter(value);
        if (derivedConverter is null)
        {
            writer.WriteStartObject();
            writer.WriteEndObject();
            return;
        }

        derivedConverter.WriteAsObject(writer, value, options);
    }

    public override object? ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.GetString();

    public override void WriteAsPropertyName(Utf8JsonWriter writer, object? value, JsonSerializerOptions options)
        => writer.WritePropertyName(value?.ToString() ?? "<null>");

    private JsonConverter? GetDerivedConverter(object value)
    {
        Type runtimeType = value.GetType();
        if (runtimeType == typeof(object))
        {
            return null;
        }

        return _derivedTypes.GetOrAdd(runtimeType, static (t, provider) => JsonSerializerTS.CreateConverter(provider.Resolve(t)), shapeProvider);
    }
}
