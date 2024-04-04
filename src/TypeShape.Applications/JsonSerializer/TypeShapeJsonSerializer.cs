using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace TypeShape.Applications.JsonSerializer;

public static partial class TypeShapeJsonSerializer
{
    private readonly static JsonSerializerOptions s_options = new() { TypeInfoResolver = JsonTypeInfoResolver.Combine() };

    public static TypeShapeJsonSerializer<T> Create<T>(ITypeShape<T> shape)
    {
        var builder = new Builder(s_options);
        JsonConverter<T> converter = builder.BuildConverter(shape);
        return new TypeShapeJsonSerializer<T>(converter, s_options);
    }

    public static string Serialize<T>(T? value) where T : ITypeShapeProvider<T> => 
        SerializerCache<T, T>.Value.Serialize(value);

    public static T? Deserialize<T>(string json) where T : ITypeShapeProvider<T> => 
        SerializerCache<T, T>.Value.Deserialize(json);

    public static string Serialize<T, TProvider>(T? value) where TProvider : ITypeShapeProvider<T> => 
        SerializerCache<T, TProvider>.Value.Serialize(value);

    public static T? Deserialize<T, TProvider>(string json) where TProvider : ITypeShapeProvider<T> => 
        SerializerCache<T, TProvider>.Value.Deserialize(json);

    private static class SerializerCache<T, TProvider> where TProvider : ITypeShapeProvider<T>
    {
        public static TypeShapeJsonSerializer<T> Value => s_value ??= Create(TProvider.GetShape());
        private static TypeShapeJsonSerializer<T>? s_value;
    }
}

public sealed class TypeShapeJsonSerializer<T>
{
    private readonly JsonTypeInfo<T?> _jsonTypeInfo;

    internal TypeShapeJsonSerializer(JsonConverter<T> converter, JsonSerializerOptions options)
    {
        _jsonTypeInfo = JsonMetadataServices.CreateValueInfo<T?>(options, converter);
    }

    public string Serialize(T? value) => 
        System.Text.Json.JsonSerializer.Serialize(value, _jsonTypeInfo);

    public T? Deserialize(string json) => 
        System.Text.Json.JsonSerializer.Deserialize(json, _jsonTypeInfo);
}
