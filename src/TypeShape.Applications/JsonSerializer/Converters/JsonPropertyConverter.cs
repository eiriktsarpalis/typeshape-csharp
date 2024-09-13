using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeShape.Abstractions;

namespace TypeShape.Applications.JsonSerializer.Converters;

internal abstract class JsonPropertyConverter<TDeclaringType>(string name)
{
    public string Name { get; } = name;
    public JsonEncodedText EncodedName { get; } = JsonEncodedText.Encode(name);
    public abstract bool HasGetter { get; }
    public abstract bool HasSetter { get; }
    public bool IsConstructorParameter { get; private protected init; }

    public abstract void Read(ref Utf8JsonReader reader, ref TDeclaringType declaringType, JsonSerializerOptions options);
    public abstract void Write(Utf8JsonWriter writer, ref TDeclaringType declaringType, JsonSerializerOptions options);
}

internal sealed class JsonPropertyConverter<TDeclaringType, TPropertyType> : JsonPropertyConverter<TDeclaringType>
{
    private readonly JsonConverter<TPropertyType> _propertyTypeConverter;
    private readonly Getter<TDeclaringType, TPropertyType>? _getter;
    private readonly Setter<TDeclaringType, TPropertyType>? _setter;
    private readonly bool _getterDisallowsNull;
    private readonly bool _setterDisallowsNull;

    public JsonPropertyConverter(IPropertyShape<TDeclaringType, TPropertyType> property, JsonConverter<TPropertyType> propertyTypeConverter)
        : base(property.Name)
    {
        _propertyTypeConverter = propertyTypeConverter;
        _getterDisallowsNull = property.IsGetterNonNullable;
        _setterDisallowsNull = property.IsSetterNonNullable;

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
        : base(parameter.Name)
    {
        _propertyTypeConverter = propertyConverter;
        _setterDisallowsNull = parameter.IsNonNullable;
        _setter = parameter.GetSetter();
        IsConstructorParameter = parameter.Kind is ConstructorParameterKind.ConstructorParameter;
    }

    public override bool HasGetter => _getter != null;
    public override bool HasSetter => _setter != null;

    public override void Read(ref Utf8JsonReader reader, ref TDeclaringType declaringType, JsonSerializerOptions options)
    {
        Debug.Assert(_setter != null);

        TPropertyType? result = _propertyTypeConverter.Read(ref reader, typeof(TPropertyType), options);
        if (result is null && _setterDisallowsNull)
        {
            Throw();
            void Throw() => JsonHelpers.ThrowJsonException($"The property '{Name}' cannot be set to null.");
        }

        _setter(ref declaringType, result!);
    }

    public override void Write(Utf8JsonWriter writer, ref TDeclaringType declaringType, JsonSerializerOptions options)
    {
        Debug.Assert(_getter != null);

        TPropertyType value = _getter(ref declaringType);
        if (value is null && _getterDisallowsNull)
        {
            Throw();
            void Throw() => JsonHelpers.ThrowJsonException($"The property '{Name}' cannot contain null.");
        }

        _propertyTypeConverter.Write(writer, value, options);
    }
}
