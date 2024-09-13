using System.Buffers;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace TypeShape.Applications.JsonSerializer.Converters;

internal abstract class JsonPropertyDictionary<TDeclaringType>
{
    public abstract JsonPropertyConverter<TDeclaringType>? LookupProperty(ref Utf8JsonReader reader);
}

internal static class JsonPropertyDictionary
{
    public static JsonPropertyDictionary<TDeclaringType> ToJsonPropertyDictionary<TDeclaringType>(this IEnumerable<JsonPropertyConverter<TDeclaringType>> propertiesToRead, bool isCaseSensitive)
        => isCaseSensitive
        ? new CaseSensitivePropertyDictionary<TDeclaringType>(propertiesToRead)
        : new CaseInsensitivePropertyDictionary<TDeclaringType>(propertiesToRead);

    private sealed class CaseSensitivePropertyDictionary<TDeclaringType>(
        IEnumerable<JsonPropertyConverter<TDeclaringType>> propertiesToRead) : JsonPropertyDictionary<TDeclaringType>
    {
        private readonly SpanDictionary<byte, JsonPropertyConverter<TDeclaringType>> _dict = propertiesToRead.ToSpanDictionary(p => Encoding.UTF8.GetBytes(p.Name), ByteSpanEqualityComparer.Ordinal);

        public override JsonPropertyConverter<TDeclaringType>? LookupProperty(ref Utf8JsonReader reader)
        {
            Debug.Assert(reader.TokenType is JsonTokenType.PropertyName);
            Debug.Assert(!reader.HasValueSequence);

            if (_dict.Count == 0)
            {
                return null;
            }

            scoped ReadOnlySpan<byte> source;
            byte[]? rentedBuffer = null;
            int bytesWritten = 0;

            if (!reader.ValueIsEscaped)
            {
                source = reader.ValueSpan;
            }
            else
            {
                Span<byte> tmpBuffer = reader.ValueSpan.Length <= 128
                    ? stackalloc byte[128]
                    : rentedBuffer = ArrayPool<byte>.Shared.Rent(reader.ValueSpan.Length);

                bytesWritten = reader.CopyString(tmpBuffer);
                source = tmpBuffer[..bytesWritten];
            }

            _dict.TryGetValue(source, out JsonPropertyConverter<TDeclaringType>? result);

            if (rentedBuffer != null)
            {
                rentedBuffer.AsSpan(0, bytesWritten).Clear();
                ArrayPool<byte>.Shared.Return(rentedBuffer);
            }

            return result;
        }
    }

    private sealed class CaseInsensitivePropertyDictionary<TDeclaringType>(IEnumerable<JsonPropertyConverter<TDeclaringType>> propertiesToRead) : JsonPropertyDictionary<TDeclaringType>
    {
        // Currently, the easiest way to calculate case-insensitive hash code and equality is to transcode to UTF-16 so do that.
        private readonly SpanDictionary<char, JsonPropertyConverter<TDeclaringType>> _dict = propertiesToRead.ToSpanDictionary(p => p.Name.ToCharArray(), CharSpanEqualityComparer.OrdinalIgnoreCase);

        public override JsonPropertyConverter<TDeclaringType>? LookupProperty(ref Utf8JsonReader reader)
        {
            Debug.Assert(reader.TokenType is JsonTokenType.PropertyName);
            Debug.Assert(!reader.HasValueSequence);

            if (_dict.Count == 0)
            {
                return null;
            }

            char[]? rentedBuffer = null;

            Span<char> tmpBuffer = reader.ValueSpan.Length <= 128
                ? stackalloc char[128]
                : rentedBuffer = ArrayPool<char>.Shared.Rent(reader.ValueSpan.Length);

            int charsWritten = reader.CopyString(tmpBuffer);
            ReadOnlySpan<char> source = tmpBuffer[..charsWritten];

            _dict.TryGetValue(source, out JsonPropertyConverter<TDeclaringType>? result);

            if (rentedBuffer != null)
            {
                rentedBuffer.AsSpan(0, charsWritten).Clear();
                ArrayPool<char>.Shared.Return(rentedBuffer);
            }

            return result;
        }
    }
}