namespace TypeShape;

/// <summary>
/// When applied on a partial class, this attribute instructs the 
/// TypeShape source generator to emit an <see cref="ITypeShapeProvider"/> 
/// implementation that includes a shape definition for <see cref="Type"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class GenerateShapeAttribute : Attribute
{
    /// <summary>
    /// Creates an attribute instance.
    /// </summary>
    /// <param name="type">The type for which to generate a shape.</param>
    public GenerateShapeAttribute(Type type)
    {
        Type = type;
    }

    /// <summary>
    /// The type for which to generate a shape.
    /// </summary>
    public Type Type { get; }
}
