using System.Reflection;

namespace TypeShape.SourceGenModel;

public sealed class SourceGenConstructorShape<TDeclaringType, TArgumentState> : IConstructorShape<TDeclaringType, TArgumentState>
{
    public required bool IsPublic { get; init; }
    public required ITypeShape<TDeclaringType> DeclaringType { get; init; }
    public required int ParameterCount { get; init; }
    public Func<ICustomAttributeProvider?>? AttributeProviderFunc { get; init; }
    public ICustomAttributeProvider? AttributeProvider => AttributeProviderFunc?.Invoke();

    public Func<IEnumerable<IConstructorParameterShape>>? GetParametersFunc { get; init; }

    public Func<TDeclaringType>? DefaultConstructorFunc { get; init; }
    public required Func<TArgumentState> ArgumentStateConstructorFunc { get; init; }
    public required Constructor<TArgumentState, TDeclaringType> ParameterizedConstructorFunc { get; init; }

    public IEnumerable<IConstructorParameterShape> GetParameters()
        => GetParametersFunc?.Invoke() ?? [];

    public Func<TDeclaringType> GetDefaultConstructor()
        => DefaultConstructorFunc ?? throw new InvalidOperationException("Constructor shape does not specify a default constructor.");

    public Func<TArgumentState> GetArgumentStateConstructor()
        => ArgumentStateConstructorFunc;

    public Constructor<TArgumentState, TDeclaringType> GetParameterizedConstructor()
        => ParameterizedConstructorFunc;
}
