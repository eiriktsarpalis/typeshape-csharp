using System.Reflection;

namespace TypeShape.SourceGenModel;

public sealed class SourceGenConstructorShape<TDeclaringType, TArgumentState> : IConstructorShape<TDeclaringType, TArgumentState>
{
    public required ITypeShape DeclaringType { get; init; }
    public required int ParameterCount { get; init; }
    public Func<ICustomAttributeProvider?>? AttributeProviderFunc { get; init; }
    public ICustomAttributeProvider? AttributeProvider => AttributeProviderFunc?.Invoke();

    public Func<IEnumerable<IConstructorParameterShape>>? GetParametersFunc { get; init; }

    public Func<TDeclaringType>? DefaultConstructorFunc { get; init; }
    public required Func<TArgumentState> ArgumentStateConstructorFunc { get; init; }
    public required Constructor<TArgumentState, TDeclaringType> ParameterizedConstructorFunc { get; init; }

    public object? Accept(ITypeShapeVisitor visitor, object? state)
        => visitor.VisitConstructor(this, state);

    public IEnumerable<IConstructorParameterShape> GetParameters()
        => GetParametersFunc is null ? [] : GetParametersFunc();

    public Func<TDeclaringType> GetDefaultConstructor()
    {
        if (DefaultConstructorFunc is null)
        {
            throw new InvalidOperationException("Constructor shape does not specify a default constructor.");
        }

        return DefaultConstructorFunc;
    }

    public Func<TArgumentState> GetArgumentStateConstructor()
    {
        return ArgumentStateConstructorFunc;
    }

    public Constructor<TArgumentState, TDeclaringType> GetParameterizedConstructor()
    {
        return ParameterizedConstructorFunc;
    }
}
