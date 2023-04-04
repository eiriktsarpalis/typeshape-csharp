namespace TypeShape;

/// <summary>
/// Provides a strongly-typed shape model for a .NET enum.
/// </summary>
public interface IEnumShape
{
    /// <summary>
    /// The shape of the current enum type.
    /// </summary>
    ITypeShape Type { get; }

    /// <summary>
    /// The shape of the underlying type used to represent the enum.
    /// </summary>
    ITypeShape UnderlyingType { get; }

    /// <summary>
    /// Accepts an <see cref="ITypeShapeVisitor"/> for strongly-typed traversal.
    /// </summary>
    /// <param name="visitor">The visitor to accept.</param>
    /// <param name="state">The state parameter to pass to the underlying visitor.</param>
    /// <returns>The <see cref="object?"/> result returned by the visitor.</returns>
    object? Accept(ITypeShapeVisitor visitor, object? state);
}

/// <summary>
/// Provides a strongly-typed shape model for a .NET enum.
/// </summary>
/// <typeparam name="TEnum">The type of .NET enum.</typeparam>
/// <typeparam name="TUnderlying">The underlying type used to represent the enum.</typeparam>
public interface IEnumShape<TEnum, TUnderlying> : IEnumShape
    where TEnum : struct, Enum
{
}