using System.Reflection;

namespace TypeShape;

public interface IConstructorParameter
{
    int Position { get; }
    IType ParameterType { get; }
    string? Name { get; }
    bool HasDefaultValue { get; }
    object? DefaultValue { get; }
    bool IsRequired { get; }
    ICustomAttributeProvider? AttributeProvider { get; }
    object? Accept(IConstructorParameterVisitor visitor, object? state);
}

public interface IConstructorParameter<TArgumentState, TParameter> : IConstructorParameter
{
    Setter<TArgumentState, TParameter> GetSetter();
    new TParameter? DefaultValue { get; }
}

public interface IConstructorParameterVisitor
{
    object? VisitConstructorParameter<TArgumentState, TParameter>(IConstructorParameter<TArgumentState, TParameter> parameter, object? state);
}
