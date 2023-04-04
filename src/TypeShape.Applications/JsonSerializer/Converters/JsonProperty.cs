namespace TypeShape.Applications.JsonSerializer.Converters;

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

internal abstract class JsonProperty<TDeclaringType>
{
    public abstract string Name { get; }
    public abstract bool CanRead { get; }
    public abstract bool CanWrite { get; }

    public abstract void Deserialize(ref Utf8JsonReader reader, ref TDeclaringType declaringType, JsonSerializerOptions options);
    public abstract void Serialize(Utf8JsonWriter writer, ref TDeclaringType declaringType, JsonSerializerOptions options);
}

internal sealed class JsonProperty<TDeclaringType, TPropertyType> : JsonProperty<TDeclaringType>
{
    private readonly JsonConverter<TPropertyType> _propertyConverter;
    private readonly Getter<TDeclaringType, TPropertyType>? _getter;
    private readonly Setter<TDeclaringType, TPropertyType>? _setter;

    public JsonProperty(IPropertyShape<TDeclaringType, TPropertyType> property, JsonConverter<TPropertyType> propertyConverter)
    {
        Name = property.Name;
        _propertyConverter = propertyConverter;

        if (property.HasGetter)
        {
            _getter = property.GetGetter();
        }

        if (property.HasSetter)
        {
            _setter = property.GetSetter();
        }
    }

    public JsonProperty(IConstructorParameterShape<TDeclaringType, TPropertyType> parameter, JsonConverter<TPropertyType> propertyConverter)
    {
        Name = parameter.Name!;
        _propertyConverter = propertyConverter;
        _setter = parameter.GetSetter();
    }

    public override string Name { get; }
    public override bool CanRead => _setter != null;
    public override bool CanWrite => _getter != null;

    public override void Deserialize(ref Utf8JsonReader reader, ref TDeclaringType declaringType, JsonSerializerOptions options)
    {
        Debug.Assert(_setter != null);

        TPropertyType? result = _propertyConverter.Read(ref reader, typeof(TPropertyType), options);
        _setter(ref declaringType, result!);
    }

    public override void Serialize(Utf8JsonWriter writer, ref TDeclaringType declaringType, JsonSerializerOptions options)
    {
        Debug.Assert(_getter != null);

        writer.WritePropertyName(Name);
        TPropertyType value = _getter(ref declaringType);
        _propertyConverter.Write(writer, value, options);
    }
}
