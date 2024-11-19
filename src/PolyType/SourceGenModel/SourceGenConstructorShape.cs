using System.Reflection;
using PolyType.Abstractions;

namespace PolyType.SourceGenModel;

/// <summary>
/// Source generator model for a constructor shape.
/// </summary>
/// <typeparam name="TDeclaringType">The type being constructed.</typeparam>
/// <typeparam name="TArgumentState">The mutable argument state for the constructor.</typeparam>
public sealed class SourceGenConstructorShape<TDeclaringType, TArgumentState> : IConstructorShape<TDeclaringType, TArgumentState>
{
    /// <summary>
    /// Gets whether the constructor is public.
    /// </summary>
    public required bool IsPublic { get; init; }

    /// <summary>
    /// Gets the shape of the declaring type.
    /// </summary>
    public required IObjectTypeShape<TDeclaringType> DeclaringType { get; init; }

    /// <summary>
    /// Gets the number of parameters the constructor takes.
    /// </summary>
    public required int ParameterCount { get; init; }

    /// <summary>
    /// Gets the attribute provider for the constructor.
    /// </summary>
    public Func<ICustomAttributeProvider?>? AttributeProviderFunc { get; init; }

    /// <summary>
    /// Gets the parameter shapes for the constructor.
    /// </summary>
    public Func<IEnumerable<IConstructorParameterShape>>? GetParametersFunc { get; init; }

    /// <summary>
    /// Gets the default constructor for the declaring type.
    /// </summary>
    public Func<TDeclaringType>? DefaultConstructorFunc { get; init; }

    /// <summary>
    /// Gets the argument state constructor for the constructor.
    /// </summary>
    public Func<TArgumentState>? ArgumentStateConstructorFunc { get; init; }

    /// <summary>
    /// Gets the parameterized constructor for the constructor.
    /// </summary>
    public Constructor<TArgumentState, TDeclaringType>? ParameterizedConstructorFunc { get; init; }

    IEnumerable<IConstructorParameterShape> IConstructorShape.GetParameters()
        => GetParametersFunc?.Invoke() ?? [];

    Func<TDeclaringType> IConstructorShape<TDeclaringType, TArgumentState>.GetDefaultConstructor()
        => DefaultConstructorFunc ?? throw new InvalidOperationException("Constructor shape does not specify a default constructor.");

    Func<TArgumentState> IConstructorShape<TDeclaringType, TArgumentState>.GetArgumentStateConstructor()
        => ArgumentStateConstructorFunc ?? throw new InvalidOperationException("Constructor shape does not specify a parameterized constructor.");

    Constructor<TArgumentState, TDeclaringType> IConstructorShape<TDeclaringType, TArgumentState>.GetParameterizedConstructor()
        => ParameterizedConstructorFunc ?? throw new InvalidOperationException("Constructor shape does not specify a parameterized constructor.");

    object? IConstructorShape.Accept(ITypeShapeVisitor visitor, object? state) => visitor.VisitConstructor(this, state);
    ICustomAttributeProvider? IConstructorShape.AttributeProvider => AttributeProviderFunc?.Invoke();
    IObjectTypeShape IConstructorShape.DeclaringType => DeclaringType;
}
