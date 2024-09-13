using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TypeShape.Applications.JsonSerializer.Converters;

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
            return Parse(destination[..charLength]);
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
        writer.WriteRawValue(destination[..charsWritten]);

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

public sealed class JsonObjectConverter(ITypeShapeProvider provider) : JsonConverter<object?>
{
    private static readonly ConcurrentDictionary<Type, ITypeShapeJsonConverter> _derivedTypes = new();
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

        ITypeShapeJsonConverter? derivedConverter = GetDerivedConverter(value);
        if (derivedConverter is null)
        {
            writer.WriteStartObject();
            writer.WriteEndObject();
            return;
        }

        derivedConverter.Write(writer, value);
    }

    public override object? ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.GetString();

    public override void WriteAsPropertyName(Utf8JsonWriter writer, object? value, JsonSerializerOptions options)
        => writer.WritePropertyName(value?.ToString() ?? "<null>");

    private ITypeShapeJsonConverter? GetDerivedConverter(object value)
    {
        Type runtimeType = value.GetType();
        if (runtimeType == typeof(object))
        {
            return null;
        }

        return _derivedTypes.GetOrAdd(runtimeType, TypeShapeJsonSerializer.CreateConverter, provider);
    }
}
