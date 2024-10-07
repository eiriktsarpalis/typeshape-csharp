using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TypeShape.Examples.JsonSerializer;

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

    public static Span<byte> DecodeToUtf8UsingRentedBuffer(ReadOnlySpan<char> json, out byte[] rentedBuffer)
    {
        int maxCount = Encoding.UTF8.GetMaxByteCount(json.Length);
        rentedBuffer = ArrayPool<byte>.Shared.Rent(maxCount);
        int length = Encoding.UTF8.GetBytes(json, rentedBuffer);
        return rentedBuffer.AsSpan(0, length);
    }

    [ThreadStatic]
    private static ThreadStaticWriteState? t_writeState;

    public static Utf8JsonWriter GetPooledJsonWriter(JsonWriterOptions options, out ArrayBufferWriter<byte> bufferWriter)
    {
        ThreadStaticWriteState writeState = t_writeState ??= new(initialCapacity: 512);
        if (writeState.Depth++ == 0)
        {
            bufferWriter = writeState.BufferWriter;
            writeState.JsonWriter.Reset(bufferWriter, options);
            return writeState.JsonWriter;
        }

        bufferWriter = new(initialCapacity: 512);
        return new(bufferWriter, options);
    }

    public static void ReturnPooledJsonWriter(Utf8JsonWriter writer, ArrayBufferWriter<byte> bufferWriter)
    {
        ThreadStaticWriteState? writeState = t_writeState;
        Debug.Assert(writeState != null);
        Debug.Assert(writeState.JsonWriter == writer && writeState.BufferWriter == bufferWriter);
        bufferWriter.ResetWrittenCount();
        writer.Reset();
        writeState.Depth--;
    }

    private sealed class ThreadStaticWriteState
    {
        public ThreadStaticWriteState(int initialCapacity)
        {
            BufferWriter = new(initialCapacity);
            JsonWriter = new(BufferWriter);
        }

        public int Depth;
        public Utf8JsonWriter JsonWriter { get; }
        public ArrayBufferWriter<byte> BufferWriter { get; }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "Reset")]
    public static extern void Reset(this Utf8JsonWriter writer, IBufferWriter<byte> bufferWriter, JsonWriterOptions options);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "WriteAsObject")]
    public static extern void WriteAsObject(this JsonConverter converter, Utf8JsonWriter writer, object? value, JsonSerializerOptions options);
}
