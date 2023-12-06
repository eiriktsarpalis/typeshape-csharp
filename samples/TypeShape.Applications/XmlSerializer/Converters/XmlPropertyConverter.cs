using System.Diagnostics;
using System.Xml;

namespace TypeShape.Applications.XmlSerializer.Converters;

internal abstract class XmlPropertyConverter<TDeclaringType>(string name)
{
    public string Name { get; } = name;
    public abstract bool HasGetter { get; }
    public abstract bool HasSetter { get; }
    public bool IsConstructorParameter { get; private protected init; }

    public abstract void Write(XmlWriter writer, ref TDeclaringType value);
    public abstract void Read(XmlReader reader, ref TDeclaringType value);
}

internal sealed class XmlPropertyConverter<TDeclaringType, TPropertyType> : XmlPropertyConverter<TDeclaringType>
{
    private readonly XmlConverter<TPropertyType> _propertyConverter;
    private readonly Getter<TDeclaringType, TPropertyType>? _getter;
    private readonly Setter<TDeclaringType, TPropertyType>? _setter;

    public XmlPropertyConverter(IPropertyShape<TDeclaringType, TPropertyType> property, XmlConverter<TPropertyType> propertyConverter)
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

    public XmlPropertyConverter(IConstructorParameterShape<TDeclaringType, TPropertyType> parameter, XmlConverter<TPropertyType> propertyConverter)
    : base(parameter.Name)
    {
        _propertyConverter = propertyConverter;
        _setter = parameter.GetSetter();
        IsConstructorParameter = parameter.Kind is ConstructorParameterKind.ConstructorParameter;
    }

    public override bool HasGetter => _getter != null;
    public override bool HasSetter => _setter != null;

    public override void Read(XmlReader reader, ref TDeclaringType declaringType)
    {
        Debug.Assert(_setter != null);

        TPropertyType? result = _propertyConverter.Read(reader);
        _setter(ref declaringType, result!);
    }

    public override void Write(XmlWriter writer, ref TDeclaringType declaringType)
    {
        Debug.Assert(_getter != null);

        TPropertyType value = _getter(ref declaringType);
        _propertyConverter.Write(writer, Name, value);
    }
}