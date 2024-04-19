using TypeShape.Abstractions;

namespace TypeShape.Applications.JsonSerializer;

public static partial class TypeShapeJsonSerializer
{
    public static TypeShapeJsonConverter<T> CreateConverter<T>(ITypeShape<T> shape) =>
        new Builder().BuildTypeShapeJsonConverter(shape);

    public static TypeShapeJsonConverter<T> CreateConverter<T>(ITypeShapeProvider provider) =>
        CreateConverter(provider.Resolve<T>());

    public static string Serialize<T>(T? value) where T : ITypeShapeProvider<T> => 
        SerializerCache<T, T>.Value.Serialize(value);

    public static T? Deserialize<T>(string json) where T : ITypeShapeProvider<T> => 
        SerializerCache<T, T>.Value.Deserialize(json);

    public static string Serialize<T, TProvider>(T? value) where TProvider : ITypeShapeProvider<T> => 
        SerializerCache<T, TProvider>.Value.Serialize(value);

    public static T? Deserialize<T, TProvider>(string json) where TProvider : ITypeShapeProvider<T> => 
        SerializerCache<T, TProvider>.Value.Deserialize(json);

    internal static ITypeShapeJsonConverter CreateConverter(Type type, ITypeShapeProvider provider)
    {
        ITypeShape shape = provider.Resolve(type);
        ITypeShapeFunc builder = new Builder();
        return (ITypeShapeJsonConverter)shape.Invoke(builder)!;
    }

    private static class SerializerCache<T, TProvider> where TProvider : ITypeShapeProvider<T>
    {
        public static TypeShapeJsonConverter<T> Value => s_value ??= CreateConverter(TProvider.GetShape());
        private static TypeShapeJsonConverter<T>? s_value;
    }
}