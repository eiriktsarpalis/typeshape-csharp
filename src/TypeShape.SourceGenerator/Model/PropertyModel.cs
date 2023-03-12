namespace TypeShape.SourceGenerator.Model;

public sealed record PropertyModel
{
    public required string Name { get; init; }
    public required TypeId DeclaringType { get; init; }
    public required TypeId PropertyType { get; init; }
    
    public bool EmitGetter { get; init; }
    public bool EmitSetter { get; init; }
    
    public bool IsRequired { get; init; }
    public bool IsInitOnly { get; init; }
    public bool IsField { get; init; }
}
