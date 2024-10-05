namespace TypeShape;

/// <summary>
/// When applied on a partial type, this attribute instructs the
/// TypeShape source generator to emit an <see cref="IShapeable{T}"/>
/// implementation for the annotated type.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
public sealed class GenerateShapeAttribute : Attribute;

/// <summary>
/// When applied on a partial class, instructs the TypeShape source generator
/// to emit an <see cref="ITypeShapeProvider"/> implementation that
/// includes a shape definition for <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The type for which shape metadata will be generated.</typeparam>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class GenerateShapeAttribute<T> : Attribute;