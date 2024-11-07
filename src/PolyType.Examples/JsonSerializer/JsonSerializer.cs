using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PolyType.Abstractions;

namespace PolyType.Examples.JsonSerializer;

/// <summary>
/// Provides an JSON serialization implementation built on top of PolyType.
/// </summary>
public static partial class JsonSerializerTS
{
    private static readonly JsonSerializerOptions s_options = new();

    /// <summary>
    /// Builds a <see cref="JsonConverter{T}"/> instance from the specified shape.
    /// </summary>
    /// <typeparam name="T">The type for which to build the converter.</typeparam>
    /// <param name="shape">The shape instance guiding converter construction.</param>
    /// <returns>An <see cref="JsonConverter{T}"/> instance.</returns>
    public static JsonConverter<T> CreateConverter<T>(ITypeShape<T> shape) =>
        new Builder().BuildJsonConverter(shape);

    /// <summary>
    /// Builds an <see cref="JsonConverter{T}"/> instance from the specified shape provider.
    /// </summary>
    /// <typeparam name="T">The type for which to build the converter.</typeparam>
    /// <param name="shapeProvider">The shape provider guiding converter construction.</param>
    /// <returns>An <see cref="JsonConverter{T}"/> instance.</returns>
    public static JsonConverter<T> CreateConverter<T>(ITypeShapeProvider shapeProvider) =>
        CreateConverter(shapeProvider.Resolve<T>());

    /// <summary>
    /// Builds a <see cref="JsonConverter"/> instance from the specified shape provider.
    /// </summary>
    /// <param name="shape">The shape instance guiding converter construction.</param>
    /// <returns>An <see cref="JsonConverter{T}"/> instance.</returns>
    public static JsonConverter CreateConverter(ITypeShape shape)
    {
        ITypeShapeFunc builder = new Builder();
        return (JsonConverter)shape.Invoke(builder)!;
    }

    /// <summary>
    /// Builds an <see cref="JsonConverter{T}"/> instance using its <see cref="IShapeable{T}"/> implementation.
    /// </summary>
    /// <typeparam name="T">The type for which to build the converter.</typeparam>
    /// <returns>An <see cref="JsonConverter{T}"/> instance.</returns>
    public static JsonConverter<T> CreateConverter<T>() where T : IShapeable<T> =>
        CreateConverter(T.GetShape());

    /// <summary>
    /// Serializes a value to a JSON string using the provided converter.
    /// </summary>
    /// <typeparam name="T">The type of the value to serialize.</typeparam>
    /// <param name="converter">The converter used to serialize the value.</param>
    /// <param name="value">The value to be serialized.</param>
    /// <param name="options">The options object guiding JSON formatting.</param>
    /// <returns>A JSON encoded string containing the serialized value.</returns>
    public static string Serialize<T>(this JsonConverter<T> converter, T? value, JsonWriterOptions options = default)
    {
        Utf8JsonWriter writer = JsonHelpers.GetPooledJsonWriter(options, out ArrayBufferWriter<byte> bufferWriter);
        try
        {
            converter.Serialize(writer, value);
            return Encoding.UTF8.GetString(bufferWriter.WrittenSpan);
        }
        finally
        {
            JsonHelpers.ReturnPooledJsonWriter(writer, bufferWriter);
        }
    }

    /// <summary>
    /// Serializes a value to JSON using the provided converter.
    /// </summary>
    /// <typeparam name="T">The type of the value to serialize.</typeparam>
    /// <param name="converter">The converter used to serialize the value.</param>
    /// <param name="writer">The JSON writer where the value will be written.</param>
    /// <param name="value">The value to be serialized.</param>
    /// <returns>A JSON encoded string containing the serialized value.</returns>
    public static void Serialize<T>(this JsonConverter<T> converter, Utf8JsonWriter writer, T? value)
    {
        if (value is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            converter.Write(writer, value!, s_options);
        }

        writer.Flush();
    }

    /// <summary>
    /// Deserializes a value from a JSON string using the provided converter.
    /// </summary>
    /// <typeparam name="T">The type of the value to deserialize.</typeparam>
    /// <param name="converter">The converter used to deserialize the value.</param>
    /// <param name="json">The JSON encoding to be deserialized.</param>
    /// <param name="options">The options object guiding JSON reading.</param>
    /// <returns>The deserialized value.</returns>
    public static T? Deserialize<T>(this JsonConverter<T> converter, [StringSyntax(StringSyntaxAttribute.Json)] ReadOnlySpan<char> json, JsonReaderOptions options = default)
    {
        Span<byte> utf8Json = JsonHelpers.DecodeToUtf8UsingRentedBuffer(json, out byte[] rentedBuffer);
        try
        {
            return converter.Deserialize(utf8Json, options);
        }
        finally
        {
            utf8Json.Clear();
            ArrayPool<byte>.Shared.Return(rentedBuffer);
        }
    }

    /// <summary>
    /// Deserializes a value from a JSON string using the provided converter.
    /// </summary>
    /// <typeparam name="T">The type of the value to deserialize.</typeparam>
    /// <param name="converter">The converter used to deserialize the value.</param>
    /// <param name="utf8Json">The JSON encoding to be deserialized.</param>
    /// <param name="options">The options object guiding JSON reading.</param>
    /// <returns>The deserialized value.</returns>
    public static T? Deserialize<T>(this JsonConverter<T> converter, ReadOnlySpan<byte> utf8Json, JsonReaderOptions options = default)
    {
        Utf8JsonReader reader = new(utf8Json, options);
        reader.EnsureRead();
        return default(T) is null && reader.TokenType is JsonTokenType.Null ? default : converter.Read(ref reader, typeof(T), s_options);
    }

    /// <summary>
    /// Serializes a value to a JSON string using its <see cref="IShapeable{T}"/> implementation.
    /// </summary>
    /// <typeparam name="T">The type of the value to serialize.</typeparam>
    /// <param name="value">The value to be serialized.</param>
    /// <param name="options">The options object guiding JSON formatting.</param>
    /// <returns>An JSON encoded string containing the serialized value.</returns>
    public static string Serialize<T>(T? value, JsonWriterOptions options = default) where T : IShapeable<T> => 
        JsonSerializerCache<T, T>.Value.Serialize(value, options);

    /// <summary>
    /// Deserializes a value from a JSON string using its <see cref="IShapeable{T}"/> implementation.
    /// </summary>
    /// <typeparam name="T">The type of the value to deserialize.</typeparam>
    /// <param name="json">The JSON encoding to be deserialized.</param>
    /// <param name="options">The options object guiding JSON formatting.</param>
    /// <returns>The deserialized value.</returns>
    public static T? Deserialize<T>([StringSyntax(StringSyntaxAttribute.Json)] ReadOnlySpan<char> json, JsonReaderOptions options = default) where T : IShapeable<T> => 
        JsonSerializerCache<T, T>.Value.Deserialize(json, options);

    /// <summary>
    /// Serializes a value to a JSON string using an externally provided <see cref="IShapeable{T}"/> implementation.
    /// </summary>
    /// <typeparam name="T">The type of the value to serialize.</typeparam>
    /// <typeparam name="TProvider">The type providing an <see cref="IShapeable{T}"/> implementation.</typeparam>
    /// <param name="value">The value to be serialized.</param>
    /// <param name="options">The options object guiding JSON formatting.</param>
    /// <returns>A JSON encoded string containing the serialized value.</returns>
    public static string Serialize<T, TProvider>(T? value, JsonWriterOptions options = default) where TProvider : IShapeable<T> => 
        JsonSerializerCache<T, TProvider>.Value.Serialize(value, options);

    /// <summary>
    /// Deserializes a value from a JSON string using an externally provided <see cref="IShapeable{T}"/> implementation.
    /// </summary>
    /// <typeparam name="T">The type of the value to deserialize.</typeparam>
    /// <typeparam name="TProvider">The type providing an <see cref="IShapeable{T}"/> implementation.</typeparam>
    /// <param name="json">The JSON encoding to be deserialized.</param>
    /// <param name="options">The options object guiding JSON formatting.</param>
    /// <returns>The deserialized value.</returns>
    public static T? Deserialize<T, TProvider>([StringSyntax(StringSyntaxAttribute.Json)] ReadOnlySpan<char> json, JsonReaderOptions options = default) where TProvider : IShapeable<T> => 
        JsonSerializerCache<T, TProvider>.Value.Deserialize(json, options);

    private static class JsonSerializerCache<T, TProvider> where TProvider : IShapeable<T>
    {
        public static JsonConverter<T> Value => s_value ??= CreateConverter(TProvider.GetShape());
        private static JsonConverter<T>? s_value;
    }
}