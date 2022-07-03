using System.Reflection;

namespace TypeShape.SourceGenModel;

public class SourceGenConstructor<TDeclaringType, TArgumentState> : IConstructor<TDeclaringType, TArgumentState>
{
    public required IType<TDeclaringType> DeclaringType { get; init; }
    public required int ParameterCount { get; init; }
    public ICustomAttributeProvider? AttributeProvider { get; init; }

    public required Func<IEnumerable<IConstructorParameter>> GetParametersFunc { get; init; }

    public Func<TDeclaringType>? DefaultConstructorFunc { get; init; }
    public Func<TArgumentState>? ArgumentStateConstructorFunc { get; init; }
    public Func<TArgumentState, TDeclaringType>? ParameterizedConstructorFunc { get; init; }

    IType IConstructor.DeclaringType => DeclaringType;


    public object? Accept(IConstructorVisitor visitor, object? state)
        => visitor.VisitConstructor(this, state);

    public IEnumerable<IConstructorParameter> GetParameters()
        => GetParametersFunc();

    public Func<TDeclaringType> GetDefaultConstructor()
    {
        if (DefaultConstructorFunc is null)
            throw new InvalidOperationException();

        return DefaultConstructorFunc;
    }

    public Func<TArgumentState> GetArgumentStateConstructor()
    {
        if (ArgumentStateConstructorFunc is null)
            throw new InvalidOperationException();

        return ArgumentStateConstructorFunc;
    }

    public Func<TArgumentState, TDeclaringType> GetParameterizedConstructor()
    {
        if (ParameterizedConstructorFunc is null)
            throw new InvalidOperationException();

        return ParameterizedConstructorFunc;
    }
}
