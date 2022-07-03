namespace TypeShape.Applications.JsonSerializer.Converters;

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;

internal static class JsonHelpers
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void EnsureRead(this ref Utf8JsonReader reader)
    {
        if (!reader.Read())
        {
            ThrowJsonException("Could not read next JSON token.");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void EnsureTokenType(this ref Utf8JsonReader reader, JsonTokenType expectedToken)
    {
        if (reader.TokenType != expectedToken)
        {
            ThrowJsonException($"Unexpected JSON token type {reader.TokenType}.");
        }
    }

    [DoesNotReturn]
    public static void ThrowJsonException(string message) => throw new JsonException(message);
}
