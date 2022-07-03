namespace TypeShape.Applications.JsonSerializer;

using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using TypeShape.Abstractions;

public partial class TypeShapeJsonResolver : IJsonTypeInfoResolver
{
    private readonly ITypeShapeProvider _provider;
    public TypeShapeJsonResolver(ITypeShapeProvider provider)
        => _provider = provider;

    public JsonTypeInfo? GetTypeInfo(Type type, JsonSerializerOptions options)
    {
        IType? shape = _provider.GetShape(type);
        if (shape is null)
            return null;

        var visitor = new ConverterBuilder();
        var converter = (JsonConverter)shape.Accept(visitor, null)!;
        return (JsonTypeInfo)shape.Accept(s_typeInfoBuilder, (options, converter))!;
    }

    private readonly static JsonTypeInfoBuilder s_typeInfoBuilder = new();
    private sealed class JsonTypeInfoBuilder : ITypeVisitor
    {
        public object? VisitType<T>(IType<T> _, object? state)
        {
            var (options, converter) = ((JsonSerializerOptions, JsonConverter))state!;
            return JsonMetadataServices.CreateValueInfo<T>(options, converter);
        }
    }
}
