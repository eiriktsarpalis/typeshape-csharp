using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace TypeShape.Applications.JsonSerializer;

public static class TypeShapeJsonSerializer
{
    public static TypeShapeJsonSerializer<T> Create<T>(ITypeShape<T> shape)
        => new(shape);

    public static string Serialize<T>(T? value) where T : ITypeShapeProvider<T>
        => SerializerCache<T, T>.Value.Serialize(value);

    public static T? Deserialize<T>(string json) where T : ITypeShapeProvider<T>
        => SerializerCache<T, T>.Value.Deserialize(json);

    public static string Serialize<T, TProvider>(T? value) where TProvider : ITypeShapeProvider<T>
        => SerializerCache<T, TProvider>.Value.Serialize(value);

    public static T? Deserialize<T, TProvider>(string json) where TProvider : ITypeShapeProvider<T>
        => SerializerCache<T, TProvider>.Value.Deserialize(json);

    private static class SerializerCache<T, TProvider> where TProvider : ITypeShapeProvider<T>
    {
        public static TypeShapeJsonSerializer<T> Value => s_value ??= new(TProvider.GetShape());
        private static TypeShapeJsonSerializer<T>? s_value;
    }
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
