using TypeShape.Abstractions;

namespace TypeShape.Examples.JsonSerializer;

/// <summary>
/// Provides an JSON serialization implementation built on top of TypeShape.
/// </summary>
public static partial class TypeShapeJsonSerializer
{
    /// <summary>
    /// Builds a <see cref="TypeShapeJsonConverter{T}"/> instance from the specified shape.
    /// </summary>
    /// <typeparam name="T">The type for which to build the converter.</typeparam>
    /// <param name="shape">The shape instance guiding converter construction.</param>
    /// <returns>An <see cref="TypeShapeJsonConverter{T}"/> instance.</returns>
    public static TypeShapeJsonConverter<T> CreateConverter<T>(ITypeShape<T> shape) =>
        new Builder().BuildTypeShapeJsonConverter(shape);

    /// <summary>
    /// Builds an <see cref="TypeShapeJsonConverter{T}"/> instance from the specified shape provider.
    /// </summary>
    /// <typeparam name="T">The type for which to build the converter.</typeparam>
    /// <param name="shapeProvider">The shape provider guiding converter construction.</param>
    /// <returns>An <see cref="TypeShapeJsonConverter{T}"/> instance.</returns>
    public static TypeShapeJsonConverter<T> CreateConverter<T>(ITypeShapeProvider shapeProvider) =>
        CreateConverter(shapeProvider.Resolve<T>());

    /// <summary>
    /// Builds an <see cref="TypeShapeJsonConverter{T}"/> instance using its <see cref="IShapeable{T}"/> implementation.
    /// </summary>
    /// <typeparam name="T">The type for which to build the converter.</typeparam>
    /// <returns>An <see cref="TypeShapeJsonConverter{T}"/> instance.</returns>
    public static TypeShapeJsonConverter<T> CreateConverter<T>() where T : IShapeable<T> =>
        CreateConverter(T.GetShape());

    /// <summary>
    /// Serializes a value to a JSON string using its <see cref="IShapeable{T}"/> implementation.
    /// </summary>
    /// <typeparam name="T">The type of the value to serialize.</typeparam>
    /// <param name="value">The value to be serialized.</param>
    /// <returns>An JSON encoded string containing the serialized value.</returns>
    public static string Serialize<T>(T? value) where T : IShapeable<T> => 
        SerializerCache<T, T>.Value.Serialize(value);

    /// <summary>
    /// Deserializes a value from a JSON string using its <see cref="IShapeable{T}"/> implementation.
    /// </summary>
    /// <typeparam name="T">The type of the value to deserialize.</typeparam>
    /// <param name="json">The JSON encoding to be deserialized.</param>
    /// <returns>The deserialized value.</returns>
    public static T? Deserialize<T>(string json) where T : IShapeable<T> => 
        SerializerCache<T, T>.Value.Deserialize(json);

    /// <summary>
    /// Serializes a value to a JSON string using an externally provided <see cref="IShapeable{T}"/> implementation.
    /// </summary>
    /// <typeparam name="T">The type of the value to serialize.</typeparam>
    /// <typeparam name="TProvider">The type providing an <see cref="IShapeable{T}"/> implementation.</typeparam>
    /// <param name="value">The value to be serialized.</param>
    /// <returns>A JSON encoded string containing the serialized value.</returns>
    public static string Serialize<T, TProvider>(T? value) where TProvider : IShapeable<T> => 
        SerializerCache<T, TProvider>.Value.Serialize(value);

    /// <summary>
    /// Deserializes a value from a JSON string using an externally provided <see cref="IShapeable{T}"/> implementation.
    /// </summary>
    /// <typeparam name="T">The type of the value to deserialize.</typeparam>
    /// <typeparam name="TProvider">The type providing an <see cref="IShapeable{T}"/> implementation.</typeparam>
    /// <param name="json">The JSON encoding to be deserialized.</param>
    /// <returns>The deserialized value.</returns>
    public static T? Deserialize<T, TProvider>(string json) where TProvider : IShapeable<T> => 
        SerializerCache<T, TProvider>.Value.Deserialize(json);

    internal static ITypeShapeJsonConverter CreateConverter(Type type, ITypeShapeProvider shapeProvider)
    {
        ITypeShape shape = shapeProvider.Resolve(type);
        ITypeShapeFunc builder = new Builder();
        return (ITypeShapeJsonConverter)shape.Invoke(builder)!;
    }

    private static class SerializerCache<T, TProvider> where TProvider : IShapeable<T>
    {
        public static TypeShapeJsonConverter<T> Value => s_value ??= CreateConverter(TProvider.GetShape());
        private static TypeShapeJsonConverter<T>? s_value;
    }
}