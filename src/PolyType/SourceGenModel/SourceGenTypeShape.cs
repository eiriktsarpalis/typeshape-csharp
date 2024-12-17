using PolyType.Abstractions;
using System.Reflection;

namespace PolyType.SourceGenModel;

/// <summary>
/// Source generator model for type shapes.
/// </summary>
/// <typeparam name="T">The type that the shape describes.</typeparam>
public abstract class SourceGenTypeShape<T> : ITypeShape<T>
{
    /// <summary>
    /// Gets the <see cref="TypeShapeKind"/> that the current shape supports.
    /// </summary>
    public abstract TypeShapeKind Kind { get; }

    /// <summary>
    /// Gets the provider used to generate this instance.
    /// </summary>
    public required ITypeShapeProvider Provider { get; init; }

    Type ITypeShape.Type => typeof(T);
    ICustomAttributeProvider? ITypeShape.AttributeProvider => typeof(T);

    /// <summary>
    /// Accepts an <see cref="ITypeShapeVisitor"/> for strongly-typed traversal.
    /// </summary>
    /// <param name="visitor">The visitor to accept.</param>
    /// <param name="state">The state parameter to pass to the underlying visitor.</param>
    /// <returns>The <see cref="object"/> result returned by the visitor.</returns>
    public abstract object? Accept(ITypeShapeVisitor visitor, object? state = null);

    /// <inheritdoc/>
    object? ITypeShape.Invoke(ITypeShapeFunc func, object? state) => func.Invoke(this, state);
}
