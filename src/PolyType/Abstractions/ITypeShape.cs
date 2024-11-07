using System.Reflection;

namespace PolyType.Abstractions;

/// <summary>
/// Provides a strongly typed shape model for a given .NET type.
/// </summary>
public interface ITypeShape
{
    /// <summary>
    /// The underlying <see cref="Type"/> that this instance represents.
    /// </summary>
    Type Type { get; }

    /// <summary>
    /// Determines the <see cref="TypeShapeKind"/> that the current shape supports.
    /// </summary>
    TypeShapeKind Kind { get; }

    /// <summary>
    /// The provider used to generate this instance.
    /// </summary>
    public ITypeShapeProvider Provider { get; }

    /// <summary>
    /// The provider used for type-level attribute resolution.
    /// </summary>
    public ICustomAttributeProvider? AttributeProvider { get; }

    /// <summary>
    /// Accepts an <see cref="ITypeShapeVisitor"/> for strongly-typed traversal.
    /// </summary>
    /// <param name="visitor">The visitor to accept.</param>
    /// <param name="state">The state parameter to pass to the underlying visitor.</param>
    /// <returns>The <see cref="object"/> result returned by the visitor.</returns>
    object? Accept(ITypeShapeVisitor visitor, object? state = null);

    /// <summary>
    /// Invokes the specified generic function with the given state.
    /// </summary>
    /// <param name="function">The generic function to be invoked.</param>
    /// <param name="state">The state to be passed to the function.</param>
    /// <returns>The result produced by the function.</returns>
    object? Invoke(ITypeShapeFunc function, object? state = null);
}

/// <summary>
/// Provides a strongly typed shape model for a given .NET type.
/// </summary>
/// <typeparam name="T">The type that the shape describes.</typeparam>
public interface ITypeShape<T> : ITypeShape
{
    Type ITypeShape.Type => typeof(T);
    ICustomAttributeProvider ITypeShape.AttributeProvider => typeof(T);
    object? ITypeShape.Invoke(ITypeShapeFunc function, object? state) => function.Invoke(this, state);
}