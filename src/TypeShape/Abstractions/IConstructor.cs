using System.Reflection;

namespace TypeShape;

public interface IConstructor
{
    IType DeclaringType { get; }
    int ParameterCount { get; }
    ICustomAttributeProvider? AttributeProvider { get; }
    IEnumerable<IConstructorParameter> GetParameters();
    object? Accept(IConstructorVisitor visitor, object? state);
}

public interface IConstructor<TDeclaringType, TArgumentState> : IConstructor
{
    Func<TDeclaringType> GetDefaultConstructor();
    Func<TArgumentState, TDeclaringType> GetParameterizedConstructor();
    Func<TArgumentState> GetArgumentStateConstructor();
}

public interface IConstructorVisitor
{
    object? VisitConstructor<TDeclaringType, TArgumentState>(IConstructor<TDeclaringType, TArgumentState> constructor, object? state);
}