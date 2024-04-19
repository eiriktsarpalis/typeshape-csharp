using System.Diagnostics;
using System.Formats.Cbor;
using TypeShape.Abstractions;

namespace TypeShape.Applications.CborSerializer.Converters;

internal abstract class CborPropertyConverter<TDeclaringType>(string name)
{
    public string Name { get; } = name;
    public abstract bool HasGetter { get; }
    public abstract bool HasSetter { get; }
    public bool IsConstructorParameter { get; private protected init; }

    public abstract void Write(CborWriter writer, ref TDeclaringType value);
    public abstract void Read(CborReader reader, ref TDeclaringType value);
}

internal sealed class CborPropertyConverter<TDeclaringType, TPropertyType> : CborPropertyConverter<TDeclaringType>
{
    private readonly CborConverter<TPropertyType> _propertyConverter;
    private readonly Getter<TDeclaringType, TPropertyType>? _getter;
    private readonly Setter<TDeclaringType, TPropertyType>? _setter;

    public CborPropertyConverter(IPropertyShape<TDeclaringType, TPropertyType> property, CborConverter<TPropertyType> propertyConverter)
        : base(property.Name)
    {
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

    public CborPropertyConverter(IConstructorParameterShape<TDeclaringType, TPropertyType> parameter, CborConverter<TPropertyType> propertyConverter)
    : base(parameter.Name)
    {
        _propertyConverter = propertyConverter;
        _setter = parameter.GetSetter();
        IsConstructorParameter = parameter.Kind is ConstructorParameterKind.ConstructorParameter;
    }

    public override bool HasGetter => _getter != null;
    public override bool HasSetter => _setter != null;

    public override void Read(CborReader reader, ref TDeclaringType declaringType)
    {
        Debug.Assert(_setter != null);

        TPropertyType? result = _propertyConverter.Read(reader);
        _setter(ref declaringType, result!);
    }

    public override void Write(CborWriter writer, ref TDeclaringType declaringType)
    {
        Debug.Assert(_getter != null);

        TPropertyType value = _getter(ref declaringType);
        _propertyConverter.Write(writer, value);
    }
}
