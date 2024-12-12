using PolyType.Abstractions;
using PolyType.ReflectionProvider;

namespace PolyType;

/// <summary>
/// Configures the generated <see cref="ITypeShape"/> of the annotated type.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
public sealed class TypeShapeAttribute : Attribute
{
    private const TypeShapeKind Undefined = (TypeShapeKind)(-1);
    private readonly TypeShapeKind _kind = Undefined;

    /// <summary>
    /// Specifies the kind that should be generated for the annotated type.
    /// </summary>
    /// <remarks>
    /// Passing <see cref="TypeShapeKind.None"/> will result in the generation of
    /// an <see cref="IObjectTypeShape"/> that does not contain any properties or constructors.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The specified value is not a valid <see cref="TypeShapeKind"/>.</exception>
    public TypeShapeKind Kind
    {
        get => _kind;
        init
        {
            if (!ReflectionHelpers.IsEnumDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The specified value is not a valid TypeShapeKind.");
            }

            _kind = value;
        }
    }

    internal TypeShapeKind? GetRequestedKind() => _kind == Undefined ? null : _kind;
}