using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace TypeShape.Applications.JsonSerializer;

public static partial class TypeShapeJsonSerializer
{
    private readonly static JsonSerializerOptions s_options = new()
    {
        TypeInfoResolver = JsonTypeInfoResolver.Combine(), // Use an empty resolver
    };

    public static TypeShapeJsonSerializer<T> Create<T>(ITypeShape<T> shape)
    {
        var visitor = new Visitor();
        var converter = (JsonConverter<T>)shape.Accept(visitor, null)!;
        JsonTypeInfo<T?> typeInfo = JsonMetadataServices.CreateValueInfo<T?>(s_options, converter);
        return new TypeShapeJsonSerializer<T>(typeInfo);
    }

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
        public static TypeShapeJsonSerializer<T> Value => s_value ??= Create(TProvider.GetShape());
        private static TypeShapeJsonSerializer<T>? s_value;
    }
}

public sealed class TypeShapeJsonSerializer<T>
{
    private readonly JsonTypeInfo<T?> _jsonTypeInfo;

    internal TypeShapeJsonSerializer(JsonTypeInfo<T?> jsonTypeInfo)
    {
        _jsonTypeInfo = jsonTypeInfo;
    }

    public string Serialize(T? value)
        => System.Text.Json.JsonSerializer.Serialize(value, _jsonTypeInfo);

    public T? Deserialize(string json)
        => System.Text.Json.JsonSerializer.Deserialize(json, _jsonTypeInfo);
}
