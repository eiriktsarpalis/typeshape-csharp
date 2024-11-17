namespace PolyType;

/// <summary>
/// When applied on a partial type, this attribute instructs the
/// PolyType source generator to emit an <see cref="IShapeable{T}"/>
/// implementation that includes the annotated type.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
public sealed class GenerateShapeAttribute : Attribute;

/// <summary>
/// When applied on a partial class, this attribute instructs the
/// PolyType source generator to emit an <see cref="ITypeShapeProvider"/>
/// implementation that includes <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The type for which shape metadata will be generated.</typeparam>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class GenerateShapeAttribute<T> : Attribute;