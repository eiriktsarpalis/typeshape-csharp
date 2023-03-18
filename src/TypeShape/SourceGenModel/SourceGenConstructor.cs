using System.Reflection;

namespace TypeShape.SourceGenModel;

public sealed class SourceGenConstructor<TDeclaringType, TArgumentState> : IConstructor<TDeclaringType, TArgumentState>
{
    public required IType DeclaringType { get; init; }
    public required int ParameterCount { get; init; }
    public Func<ICustomAttributeProvider?>? AttributeProviderFunc { get; init; }
    public ICustomAttributeProvider? AttributeProvider => AttributeProviderFunc is { } f ? f() : null;

    public required Func<IEnumerable<IConstructorParameter>> GetParametersFunc { get; init; }

    public Func<TDeclaringType>? DefaultConstructorFunc { get; init; }
    public Func<TArgumentState>? ArgumentStateConstructorFunc { get; init; }
    public Func<TArgumentState, TDeclaringType>? ParameterizedConstructorFunc { get; init; }

    public object? Accept(IConstructorVisitor visitor, object? state)
        => visitor.VisitConstructor(this, state);

    public IEnumerable<IConstructorParameter> GetParameters()
        => GetParametersFunc is null ? Array.Empty<IConstructorParameter>() : GetParametersFunc();

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
