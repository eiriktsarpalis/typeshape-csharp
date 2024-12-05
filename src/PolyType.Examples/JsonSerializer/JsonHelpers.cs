using PolyType.Examples.Utilities;
using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PolyType.Examples.JsonSerializer;

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

    public static unsafe Span<byte> DecodeToUtf8UsingRentedBuffer(ReadOnlySpan<char> json, out byte[] rentedBuffer)
    {
        int maxCount = Encoding.UTF8.GetMaxByteCount(json.Length);
        rentedBuffer = ArrayPool<byte>.Shared.Rent(maxCount);
        int length;
#if NET
        length = Encoding.UTF8.GetBytes(json, rentedBuffer);
#else
        fixed (char* pJson = json)
        fixed (byte* pBuffer = rentedBuffer)
        {
            length = Encoding.UTF8.GetBytes(pJson, json.Length, pBuffer, maxCount);
        }
#endif
        return rentedBuffer.AsSpan(0, length);
    }

    public static unsafe string DecodeFromUtf8(ReadOnlySpan<byte> utf8Json)
    {
#if NET
        return Encoding.UTF8.GetString(utf8Json);
#else
        fixed (byte* pUtf8Json = utf8Json)
        {
            return Encoding.UTF8.GetString(pUtf8Json, utf8Json.Length);
        }
#endif
    }

    [ThreadStatic]
    private static ThreadStaticWriteState? t_writeState;

    public static Utf8JsonWriter GetPooledJsonWriter(JsonWriterOptions options, out ByteBufferWriter bufferWriter)
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

    public static void ReturnPooledJsonWriter(Utf8JsonWriter writer, ByteBufferWriter bufferWriter)
    {
        ThreadStaticWriteState? writeState = t_writeState;
        DebugExt.Assert(writeState != null);
        DebugExt.Assert(writeState.JsonWriter == writer && writeState.BufferWriter == bufferWriter);
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
        public ByteBufferWriter BufferWriter { get; }
    }

#if NET
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "Reset")]
    public static extern void Reset(this Utf8JsonWriter writer, IBufferWriter<byte> bufferWriter, JsonWriterOptions options);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "WriteAsObject")]
    public static extern void WriteAsObject(this JsonConverter converter, Utf8JsonWriter writer, object? value, JsonSerializerOptions options);
#else
    public static void Reset(this Utf8JsonWriter writer, IBufferWriter<byte> bufferWriter, JsonWriterOptions options)
    {
        (s_resetMethod ??= CreateResetMethod())(writer, bufferWriter, options);
        static Action<Utf8JsonWriter, IBufferWriter<byte>, JsonWriterOptions> CreateResetMethod()
        {
            MethodInfo resetMethod = typeof(Utf8JsonWriter).GetMethod("Reset", BindingFlags.Instance | BindingFlags.NonPublic, null, [typeof(IBufferWriter<byte>), typeof(JsonWriterOptions)], null)!;
            return (Action<Utf8JsonWriter, IBufferWriter<byte>, JsonWriterOptions>)Delegate.CreateDelegate(typeof(Action<Utf8JsonWriter, IBufferWriter<byte>, JsonWriterOptions>), resetMethod);
        }
    }

    public static void WriteAsObject(this JsonConverter converter, Utf8JsonWriter writer, object? value, JsonSerializerOptions options)
    {
        (s_writeAsObjectMethod ??= CreateWriteAsObjectMethod())(converter, writer, value, options);
        static Action<JsonConverter, Utf8JsonWriter, object?, JsonSerializerOptions> CreateWriteAsObjectMethod()
        {
            MethodInfo writeAsObjectMethod = typeof(JsonConverter).GetMethod("WriteAsObject", BindingFlags.Instance | BindingFlags.NonPublic, null, [typeof(Utf8JsonWriter), typeof(object), typeof(JsonSerializerOptions)], null)!;
            return (Action<JsonConverter, Utf8JsonWriter, object?, JsonSerializerOptions>)Delegate.CreateDelegate(typeof(Action<JsonConverter, Utf8JsonWriter, object?, JsonSerializerOptions>), writeAsObjectMethod);
        }
    }

    private static Action<Utf8JsonWriter, IBufferWriter<byte>, JsonWriterOptions>? s_resetMethod;
    private static Action<JsonConverter, Utf8JsonWriter, object?, JsonSerializerOptions>? s_writeAsObjectMethod;
#endif
}
