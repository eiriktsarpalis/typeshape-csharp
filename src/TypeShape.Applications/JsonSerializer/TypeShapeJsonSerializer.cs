using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace TypeShape.Applications.JsonSerializer;

public static class TypeShapeJsonSerializer
{
    public static TypeShapeJsonSerializer<T> Create<T>(ITypeShape<T> shape)
        => new(shape);
}

public class TypeShapeJsonSerializer<T>
{
    private readonly JsonTypeInfo<T> _jsonTypeInfo;

    public TypeShapeJsonSerializer(ITypeShape<T> shape)
    {
        Options = new JsonSerializerOptions
        {
            TypeInfoResolver = new TypeShapeJsonResolver(shape.Provider)
        };

        _jsonTypeInfo = (JsonTypeInfo<T>)Options.GetTypeInfo(typeof(T));
    }

    public JsonSerializerOptions Options { get; }

    public string Serialize(T value)
        => System.Text.Json.JsonSerializer.Serialize(value, _jsonTypeInfo);

    public T? Deserialize(string json)
        => System.Text.Json.JsonSerializer.Deserialize(json, _jsonTypeInfo);
}
