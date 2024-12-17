namespace PolyType;

/// <summary>
/// Configures the shape of a parameter for a given constructor.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
public class ParameterShapeAttribute : Attribute
{
    /// <summary>
    /// Gets a custom name to be used for the particular parameter.
    /// </summary>
    public string? Name { get; init; }
}
