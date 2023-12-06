using System.Reflection;

namespace TypeShape;

/// <summary>
/// Provides a strongly-typed shape model for a given .NET type.
/// </summary>
public interface ITypeShape
{
    /// <summary>
    /// The underlying <see cref="Type"/> that this instance represents.
    /// </summary>
    Type Type { get; }

    /// <summary>
    /// The provider used to generate this instance.
    /// </summary>
    public ITypeShapeProvider Provider { get; }

    /// <summary>
    /// The provider used for type-level attribute resolution.
    /// </summary>
    public ICustomAttributeProvider? AttributeProvider { get; }

    /// <summary>
    /// Gets all available constructor shapes for the given type.
    /// </summary>
    /// <param name="nonPublic">Determines whether non-public constructors and member initializers should be resolved.</param>
    /// <param name="includeProperties">Determines whether non-required property setters should be included as constructor parameters.</param>
    /// <param name="includeFields">Determines whether non-required fields should be included as constructor parameters.</param>
    /// <returns>An enumeration of all available constructor shapes.</returns>
    IEnumerable<IConstructorShape> GetConstructors(bool nonPublic = false, bool includeProperties = false, bool includeFields = false);

    /// <summary>
    /// Gets all available property/field shapes for the given type.
    /// </summary>
    /// <param name="nonPublic">Determines whether non-public members should be resolved.</param>
    /// <param name="includeFields">Determines whether the enumeration should include fields instead of just properties.</param>
    /// <returns>An enumeration of all available property/field shapes.</returns>
    IEnumerable<IPropertyShape> GetProperties(bool nonPublic = false, bool includeFields = false);
    
    /// <summary>
    /// Determines the <see cref="TypeKind"/> that the current shape supports.
    /// </summary>
    TypeKind Kind { get; }

    /// <summary>
    /// Resolves an enum shape view for the current type, if of applicable <see cref="TypeKind"/>.
    /// </summary>
    /// <returns>An <see cref="IEnumShape"/> for the current <see cref="Type"/>.</returns>
    IEnumShape GetEnumShape();

    /// <summary>
    /// Resolves a nullable shape view for the current type, if of applicable <see cref="TypeKind"/>.
    /// </summary>
    /// <returns>An <see cref="INullableShape"/> for the current <see cref="Type"/>.</returns>
    INullableShape GetNullableShape();

    /// <summary>
    /// Resolves an enumerable shape view for the current type, if of applicable <see cref="TypeKind"/>.
    /// </summary>
    /// <returns>An <see cref="IEnumerableShape"/> for the current <see cref="Type"/>.</returns>
    IEnumerableShape GetEnumerableShape();

    /// <summary>
    /// Resolves a dictionary shape view for the current type, if of applicable <see cref="TypeKind"/>.
    /// </summary>
    /// <returns>An <see cref="IDictionaryShape"/> for the current <see cref="Type"/>.</returns>
    IDictionaryShape GetDictionaryShape();

    /// <summary>
    /// Accepts an <see cref="ITypeShapeVisitor"/> for strongly-typed traversal.
    /// </summary>
    /// <param name="visitor">The visitor to accept.</param>
    /// <param name="state">The state parameter to pass to the underlying visitor.</param>
    /// <returns>The <see cref="object?"/> result returned by the visitor.</returns>
    object? Accept(ITypeShapeVisitor visitor, object? state);
}

/// <summary>
/// Provides a strongly-typed shape model for a given .NET type.
/// </summary>
public interface ITypeShape<T> : ITypeShape;