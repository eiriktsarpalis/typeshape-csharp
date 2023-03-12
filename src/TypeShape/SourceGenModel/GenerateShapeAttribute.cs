namespace TypeShape;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class GenerateShapeAttribute : Attribute
{
    public GenerateShapeAttribute(Type type)
    {
        Type = type;
    }

    public Type Type { get; }
}
