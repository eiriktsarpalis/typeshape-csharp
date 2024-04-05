using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace TypeShape.Applications.JsonSerializer;

public static partial class TypeShapeJsonSerializer
{
    public static TypeShapeJsonConverter<T> CreateConverter<T>(ITypeShape<T> shape)
    {
        var builder = new Builder();
        return builder.BuildTypeShapeJsonConverter(shape);
    }

    public static ITypeShapeJsonConverter CreateConverter(ITypeShape shape)
    {         
        ITypeShapeFunc builder = new Builder();
        return (ITypeShapeJsonConverter)shape.Invoke(builder)!;
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
        public static TypeShapeJsonConverter<T> Value => s_value ??= CreateConverter(TProvider.GetShape());
        private static TypeShapeJsonConverter<T>? s_value;
    }
}