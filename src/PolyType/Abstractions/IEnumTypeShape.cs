namespace PolyType.Abstractions;

/// <summary>
/// Provides a strongly typed shape model for a .NET enum.
/// </summary>
public interface IEnumTypeShape : ITypeShape
{
    /// <summary>
    /// Gets the shape of the underlying type used to represent the enum.
    /// </summary>
    ITypeShape UnderlyingType { get; }
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
    /// Gets the shape of the underlying type used to represent the enum.
    /// </summary>
    new ITypeShape<TUnderlying> UnderlyingType { get; }
}