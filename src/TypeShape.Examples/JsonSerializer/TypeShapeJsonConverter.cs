using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace TypeShape.Examples.JsonSerializer;

/// <summary>
/// Defines a strongly typed JSON to .NET converter.
/// </summary>
public sealed class TypeShapeJsonConverter<T> : ITypeShapeJsonConverter
{
    private readonly JsonTypeInfo<T?> _jsonTypeInfo;

    internal TypeShapeJsonConverter(JsonConverter<T> converter, JsonSerializerOptions options)
    {
        _jsonTypeInfo = JsonMetadataServices.CreateValueInfo<T?>(options, converter);
    }

    /// <summary>Serializes a value to a JSON string.</summary>
    public string Serialize(T? value) => System.Text.Json.JsonSerializer.Serialize(value, _jsonTypeInfo);
    /// <summary>Deserializes a JSON string in a value.</summary>
    public T? Deserialize(string json) => System.Text.Json.JsonSerializer.Deserialize(json, _jsonTypeInfo);
    /// <summary>Deserializes a UTF-8 JSON string in a value.</summary>
    public T? Deserialize(ReadOnlySpan<byte> utf8Json) => System.Text.Json.JsonSerializer.Deserialize(utf8Json, _jsonTypeInfo);
    /// <summary>Writes a value to the specified JSON writer.</summary>
    public void Write(Utf8JsonWriter writer, T? value) => ((JsonConverter<T?>)_jsonTypeInfo.Converter).Write(writer, value, _jsonTypeInfo.Options);
    /// <summary>Reads a value from a specified JSON reader.</summary>
    public T? Read(ref Utf8JsonReader reader) => ((JsonConverter<T?>)_jsonTypeInfo.Converter).Read(ref reader, typeof(T), _jsonTypeInfo.Options);

    Type ITypeShapeJsonConverter.Type => typeof(T);
    void ITypeShapeJsonConverter.Write(Utf8JsonWriter writer, object? value) => Write(writer, (T?)value);
    object? ITypeShapeJsonConverter.Read(ref Utf8JsonReader reader) => Read(ref reader);
}

internal interface ITypeShapeJsonConverter
{
    Type Type { get; }
    void Write(Utf8JsonWriter writer, object? value);
    object? Read(ref Utf8JsonReader reader);
}