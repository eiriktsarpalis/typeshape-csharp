using PolyType.Examples.Utilities;
using PolyType.Utilities;
using System.Buffers;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PolyType.Examples.JsonSerializer.Converters;

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

#if NET
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
#else
        return BigInteger.Parse(Encoding.UTF8.GetString(reader.ValueSpan), CultureInfo.InvariantCulture);
#endif
    }

    /// <inheritdoc/>
    public sealed override void Write(Utf8JsonWriter writer, BigInteger value, JsonSerializerOptions options)
    {
#if NET
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
#else
        writer.WriteRawValue(Encoding.UTF8.GetBytes(value.ToString(CultureInfo.InvariantCulture)));
#endif
    }
}

#if NET
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
#endif

internal sealed class JsonPolymorphicObjectConverter(TypeCache typeCache) : JsonConverter<object?>
{
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

        return (JsonConverter)typeCache.GetOrAdd(runtimeType)!;
    }
}
