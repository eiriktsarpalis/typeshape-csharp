namespace TypeShape;

/// <summary>
/// Provides a strongly typed shape model for a .NET enum.
/// </summary>
public interface IEnumTypeShape : ITypeShape
{
    /// <summary>
    /// The shape of the underlying type used to represent the enum.
    /// </summary>
    ITypeShape UnderlyingType { get; }

    /// <inheritdoc/>
    TypeShapeKind ITypeShape.Kind => TypeShapeKind.Enum;
}

/// <summary>
/// Provides a strongly typed shape model for a .NET enum.
/// </summary>
/// <typeparam name="TEnum">The type of .NET enum.</typeparam>
/// <typeparam name="TUnderlying">The underlying type used to represent the enum.</typeparam>
public interface IEnumTypeShape<TEnum, TUnderlying> : ITypeShape<TEnum>, IEnumTypeShape
    where TEnum : struct, Enum
{
    /// <summary>
    /// The shape of the underlying type used to represent the enum.
    /// </summary>
    new ITypeShape<TUnderlying> UnderlyingType { get; }

    /// <inheritdoc/>
    ITypeShape IEnumTypeShape.UnderlyingType => UnderlyingType;

    /// <inheritdoc/>
    object? ITypeShape.Accept(ITypeShapeVisitor visitor, object? state) => visitor.VisitEnum(this, state);
}