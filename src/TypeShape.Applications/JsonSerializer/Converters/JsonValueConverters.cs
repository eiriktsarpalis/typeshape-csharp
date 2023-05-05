using System.Buffers;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace TypeShape.Applications.JsonSerializer.Converters;

public sealed class HalfConverter : JsonConverter<Half>
{
    public override Half Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => (Half)reader.GetDouble();

    public override void Write(Utf8JsonWriter writer, Half value, JsonSerializerOptions options)
        => writer.WriteNumberValue((float)value);
}

public sealed class Int128Converter : LargeNumberValueConverter<Int128>
{
    public override Int128 Parse(ReadOnlySpan<char> value)
        => Int128.Parse(value, CultureInfo.InvariantCulture);

    public override int GetMaxCharLength(Int128 value)
        => 40; // System.Int128.MinValue.ToString().Length

    public override int Format(Int128 value, Span<char> destination)
    {
        bool success = value.TryFormat(destination, out int charsWritten, provider: CultureInfo.InvariantCulture);
        Debug.Assert(success);
        return charsWritten;
    }
}

public sealed class UInt128Converter : LargeNumberValueConverter<UInt128>
{
    public override UInt128 Parse(ReadOnlySpan<char> value)
        => UInt128.Parse(value, CultureInfo.InvariantCulture);

    public override int GetMaxCharLength(UInt128 value)
        => 39; // System.UInt128.MaxValue.ToString().Length

    public override int Format(UInt128 value, Span<char> destination)
    {
        bool success = value.TryFormat(destination, out int charsWritten, provider: CultureInfo.InvariantCulture);
        Debug.Assert(success);
        return charsWritten;
    }
}

public sealed class BigIntegerConverter : LargeNumberValueConverter<BigInteger>
{
    public override BigInteger Parse(ReadOnlySpan<char> value)
        => BigInteger.Parse(value, CultureInfo.InvariantCulture);

    public override int GetMaxCharLength(BigInteger value)
        => (int)Math.Ceiling(value.GetByteCount() * 8.0 / Math.Log10(2));

    public override int Format(BigInteger value, Span<char> destination)
    {
        bool success = value.TryFormat(destination, out int charsWritten, provider: CultureInfo.InvariantCulture);
        Debug.Assert(success);
        return charsWritten;
    }
}

public abstract class LargeNumberValueConverter<T> : JsonConverter<T>
{
    public abstract T Parse(ReadOnlySpan<char> value);
    public abstract int GetMaxCharLength(T value);
    public abstract int Format(T value, Span<char> destination);

    public sealed override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
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
            return Parse(destination.Slice(0, charLength));
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

    public sealed override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        char[]? rentedBuffer = null;
        int maxLength = GetMaxCharLength(value);

        Span<char> destination = maxLength <= 128
            ? stackalloc char[128]
            : rentedBuffer = ArrayPool<char>.Shared.Rent(maxLength);

        int charsWritten = Format(value, destination);
        writer.WriteRawValue(destination.Slice(0, charsWritten));

        if (rentedBuffer != null)
        {
            rentedBuffer.AsSpan(0, charsWritten).Clear();
            ArrayPool<char>.Shared.Return(rentedBuffer);
        }
    }
}

public sealed class RuneConverter : JsonConverter<Rune>
{
    public override Rune Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return Rune.GetRuneAt(reader.GetString()!, 0);
    }

    public override void Write(Utf8JsonWriter writer, Rune value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}

public sealed class JsonObjectConverter : JsonConverter<object?>
{
    public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => JsonMetadataServices.ObjectConverter.Read(ref reader, typeToConvert, options);

    public override void Write(Utf8JsonWriter writer, object? value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case null: writer.WriteNullValue(); break;
            case bool b: writer.WriteBooleanValue(b); break;
            case int i: writer.WriteNumberValue(i); break;
            case string s: writer.WriteStringValue(s); break;
            default:
                writer.WriteStartObject();
                writer.WriteEndObject();
                break;
        }
    }

    public override object? ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.GetString();

    public override void WriteAsPropertyName(Utf8JsonWriter writer, object? value, JsonSerializerOptions options)
        => writer.WritePropertyName(value?.ToString() ?? "<null>");
}
