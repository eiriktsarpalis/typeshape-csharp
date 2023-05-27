namespace TypeShape.Applications.JsonSerializer.Converters;

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

internal abstract class JsonPropertyConverter<TDeclaringType>
{
    public JsonPropertyConverter(string name)
    {
        Name = name;
        EncodedName = JsonEncodedText.Encode(name);
    }

    public string Name { get; }
    public JsonEncodedText EncodedName { get; }
    public abstract bool HasGetter { get; }
    public abstract bool HasSetter { get; }

    public abstract void Read(ref Utf8JsonReader reader, ref TDeclaringType declaringType, JsonSerializerOptions options);
    public abstract void Write(Utf8JsonWriter writer, ref TDeclaringType declaringType, JsonSerializerOptions options);
}

internal sealed class JsonPropertyConverter<TDeclaringType, TPropertyType> : JsonPropertyConverter<TDeclaringType>
{
    private readonly JsonConverter<TPropertyType> _propertyTypeConverter;
    private readonly Getter<TDeclaringType, TPropertyType>? _getter;
    private readonly Setter<TDeclaringType, TPropertyType>? _setter;

    public JsonPropertyConverter(IPropertyShape<TDeclaringType, TPropertyType> property, JsonConverter<TPropertyType> propertyTypeConverter)
        : base(property.Name)
    {
        _propertyTypeConverter = propertyTypeConverter;

        if (property.HasGetter)
        {
            _getter = property.GetGetter();
        }

        if (property.HasSetter)
        {
            _setter = property.GetSetter();
        }
    }

    public JsonPropertyConverter(IConstructorParameterShape<TDeclaringType, TPropertyType> parameter, JsonConverter<TPropertyType> propertyConverter)
        : base(parameter.Name!)
    {
        _propertyTypeConverter = propertyConverter;
        _setter = parameter.GetSetter();
    }

    public override bool HasGetter => _getter != null;
    public override bool HasSetter => _setter != null;

    public override void Read(ref Utf8JsonReader reader, ref TDeclaringType declaringType, JsonSerializerOptions options)
    {
        Debug.Assert(_setter != null);

        TPropertyType? result = _propertyTypeConverter.Read(ref reader, typeof(TPropertyType), options);
        _setter(ref declaringType, result!);
    }

    public override void Write(Utf8JsonWriter writer, ref TDeclaringType declaringType, JsonSerializerOptions options)
    {
        Debug.Assert(_getter != null);

        TPropertyType value = _getter(ref declaringType);
        _propertyTypeConverter.Write(writer, value, options);
    }
}
