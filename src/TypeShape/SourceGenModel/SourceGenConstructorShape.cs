using System.Reflection;

namespace TypeShape.SourceGenModel;

public sealed class SourceGenConstructorShape<TDeclaringType, TArgumentState>(bool nonPublic, bool includeProperties, bool includeFields) : IConstructorShape<TDeclaringType, TArgumentState>
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
    {
        IEnumerable<IConstructorParameterShape> parameters = GetParametersFunc?.Invoke() ?? [];

        if (!nonPublic)
        {
            parameters = parameters.Where(p => p.IsPublic || p.IsRequired);
        }

        if (!includeProperties)
        {
            parameters = parameters.Where(p => p.Kind != ConstructorParameterKind.PropertyInitializer || p.IsRequired);
        }

        if (!includeFields)
        {
            parameters = parameters.Where(p => p.Kind != ConstructorParameterKind.FieldInitializer || p.IsRequired);
        }

        return parameters;
    }

    int IConstructorShape.ParameterCount => nonPublic && includeProperties && includeFields ? ParameterCount : GetParameters().Count();

    public Func<TDeclaringType> GetDefaultConstructor()
        => DefaultConstructorFunc ?? throw new InvalidOperationException("Constructor shape does not specify a default constructor.");

    public Func<TArgumentState> GetArgumentStateConstructor()
        => ArgumentStateConstructorFunc;

    public Constructor<TArgumentState, TDeclaringType> GetParameterizedConstructor()
        => ParameterizedConstructorFunc;
}
