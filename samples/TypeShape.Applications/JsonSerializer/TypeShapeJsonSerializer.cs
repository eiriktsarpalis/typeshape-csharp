using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace TypeShape.Applications.JsonSerializer;

public static class TypeShapeJsonSerializer
{
    public static TypeShapeJsonSerializer<T> Create<T>(ITypeShape<T> shape)
        => new(shape);
}

public sealed class TypeShapeJsonSerializer<T>
{
    private readonly static JsonSerializerOptions s_options = new()
    {
        TypeInfoResolver = new EmptyResolver(),
    };

    private readonly JsonTypeInfo<T?> _jsonTypeInfo;

    public TypeShapeJsonSerializer(ITypeShape<T> shape)
    {
        JsonConverter<T> converter = ConverterBuilder.Create(shape);
        _jsonTypeInfo = JsonMetadataServices.CreateValueInfo<T?>(s_options, converter);
    }

    public string Serialize(T? value)
        => System.Text.Json.JsonSerializer.Serialize(value, _jsonTypeInfo);

    public T? Deserialize(string json)
        => System.Text.Json.JsonSerializer.Deserialize(json, _jsonTypeInfo);

    private sealed class EmptyResolver : IJsonTypeInfoResolver
    {
        public JsonTypeInfo? GetTypeInfo(Type type, JsonSerializerOptions options) => null;
    }
}
