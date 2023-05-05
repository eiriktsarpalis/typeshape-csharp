namespace TypeShape.Applications.JsonSerializer.Converters;

using System.Buffers;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

internal abstract class JsonPropertyDictionary<TDeclaringType>
{
    public abstract JsonProperty<TDeclaringType>? LookupProperty(scoped ref Utf8JsonReader reader);
}

internal static class JsonPropertyDictionary
{
    public static JsonPropertyDictionary<TDeclaringType> Create<TDeclaringType>(IEnumerable<JsonProperty<TDeclaringType>> propertiesToRead, bool isCaseSensitive)
        => isCaseSensitive
        ? new CaseSensitivePropertyDictionary<TDeclaringType>(propertiesToRead)
        : new CaseInsensitivePropertyDictionary<TDeclaringType>(propertiesToRead);

    private sealed class CaseSensitivePropertyDictionary<TDeclaringType> : JsonPropertyDictionary<TDeclaringType>
    {
        private readonly SpanDictionary<byte, JsonProperty<TDeclaringType>> _dict;

        public CaseSensitivePropertyDictionary(IEnumerable<JsonProperty<TDeclaringType>> propertiesToRead)
        {
            _dict = propertiesToRead.ToSpanDictionary(p => Encoding.UTF8.GetBytes(p.Name), ByteSpanEqualityComparer.Ordinal);
        }

        public override JsonProperty<TDeclaringType>? LookupProperty(scoped ref Utf8JsonReader reader)
        {
            Debug.Assert(reader.TokenType is JsonTokenType.PropertyName);
            Debug.Assert(!reader.HasValueSequence);

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
                    : rentedBuffer = ArrayPool<byte>.Shared.Rent(128);

                bytesWritten = reader.CopyString(tmpBuffer);
                source = tmpBuffer.Slice(0, bytesWritten);
            }

            _dict.TryGetValue(source, out JsonProperty<TDeclaringType>? result);

            if (rentedBuffer != null)
            {
                rentedBuffer.AsSpan(0, bytesWritten).Clear();
                ArrayPool<byte>.Shared.Return(rentedBuffer);
            }

            return result;
        }
    }

    private sealed class CaseInsensitivePropertyDictionary<TDeclaringType> : JsonPropertyDictionary<TDeclaringType>
    {
        private readonly SpanDictionary<char, JsonProperty<TDeclaringType>> _dict;

        public CaseInsensitivePropertyDictionary(IEnumerable<JsonProperty<TDeclaringType>> propertiesToRead)
        {
            // Currently, the easiest way to calculate case-insensitive hashcode and equality is to transcode to UTF-16 so do that.
            _dict = propertiesToRead.ToSpanDictionary(p => p.Name.ToCharArray(), CharSpanEqualityComparer.OrdinalIgnoreCase);
        }

        public override JsonProperty<TDeclaringType>? LookupProperty(scoped ref Utf8JsonReader reader)
        {
            Debug.Assert(reader.TokenType is JsonTokenType.PropertyName);
            Debug.Assert(!reader.HasValueSequence);

            char[]? rentedBuffer = null;

            Span<char> tmpBuffer = reader.ValueSpan.Length <= 128
                ? stackalloc char[128]
                : rentedBuffer = ArrayPool<char>.Shared.Rent(128);

            int charsWritten = reader.CopyString(tmpBuffer);
            ReadOnlySpan<char> source = tmpBuffer.Slice(0, charsWritten);

            _dict.TryGetValue(source, out JsonProperty<TDeclaringType>? result);

            if (rentedBuffer != null)
            {
                rentedBuffer.AsSpan(0, charsWritten).Clear();
                ArrayPool<char>.Shared.Return(rentedBuffer);
            }

            return result;
        }
    }
}