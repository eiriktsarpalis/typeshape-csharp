using System.Reflection;

namespace TypeShape;

public delegate TPropertyType Getter<TDeclaringType, TPropertyType>(ref TDeclaringType obj);
public delegate void Setter<TDeclaringType, TPropertyType>(ref TDeclaringType obj, TPropertyType value);

public interface IProperty
{
    string Name { get; }
    ICustomAttributeProvider? AttributeProvider { get; }

    IType DeclaringType { get; }
    IType PropertyType { get; }

    bool HasGetter { get; }
    bool HasSetter { get; }
    bool IsField { get; }

    object? Accept(IPropertyVisitor visitor, object? state);
}

public interface IProperty<TDeclaringType, TPropertyType> : IProperty
{
    Getter<TDeclaringType, TPropertyType> GetGetter();
    Setter<TDeclaringType, TPropertyType> GetSetter();
}

public interface IPropertyVisitor
{
    object? VisitProperty<TDeclaringType, TPropertyType>(IProperty<TDeclaringType, TPropertyType> property, object? state);
}