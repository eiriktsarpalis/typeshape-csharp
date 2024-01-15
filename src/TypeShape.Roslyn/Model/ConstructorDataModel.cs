using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace TypeShape.Roslyn;

/// <summary>
/// A constructor data model wrapping an <see cref="IMethodSymbol"/>.
/// </summary>
public readonly struct ConstructorDataModel
{
    /// <summary>
    /// The constructor symbol that this model represents.
    /// </summary>
    public required IMethodSymbol Constructor { get; init; }

    /// <summary>
    /// The declaring type of the constructor.
    /// </summary>
    public ITypeSymbol DeclaringType => Constructor.ContainingType;

    /// <summary>
    /// The parameters of the constructor.
    /// </summary>
    public required ImmutableArray<ConstructorParameterDataModel> Parameters { get; init; }

    /// <summary>
    /// The members that could or should be initialized in conjunction with this constructor.
    /// This includes required or init-only members that are not initialized by the constructor itself.
    /// </summary>
    public required ImmutableArray<PropertyDataModel> MemberInitializers { get; init; }
}