namespace PolyType;

/// <summary>
/// Instructs the PolyType source generator to include the annotated type
/// in the <see cref="ITypeShapeProvider"/> that it generates.
/// </summary>
/// <remarks>
/// For projects targeting .NET 8 or later, this additionally augments the type
/// with an implementation of IShapeable for the type.
///
/// Projects targeting older versions of .NET need to access the generated
/// <see cref="ITypeShapeProvider"/> instance through the static property
/// added to classes annotated with the <see cref="GenerateShapeAttribute{T}"/>.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
public sealed class GenerateShapeAttribute : Attribute;

/// <summary>
/// Instructs the PolyType source generator to include <typeparamref name="T"/>
/// in the <see cref="ITypeShapeProvider"/> that it generates.
/// </summary>
/// <typeparam name="T">The type for which shape metadata will be generated.</typeparam>
/// <remarks>
/// The source generator will include a static property in the annotated class pointing
/// to the <see cref="ITypeShapeProvider"/> that was generated for the entire project.
///
/// For projects targeting .NET 8 or later, this will additionally augments the class
/// with an implementation of IShapeable for <typeparamref name="T"/>.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
public sealed class GenerateShapeAttribute<T> : Attribute;