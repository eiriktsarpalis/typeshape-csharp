namespace PolyType;

/// <summary>
/// Indicates that the annotated constructor should be included in the shape model.
/// </summary>
[AttributeUsage(AttributeTargets.Constructor, AllowMultiple = false, Inherited = false)]
public sealed class ConstructorShapeAttribute : Attribute;