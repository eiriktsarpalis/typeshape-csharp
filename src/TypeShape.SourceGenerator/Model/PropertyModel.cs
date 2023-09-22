namespace TypeShape.SourceGenerator.Model;

public sealed record PropertyModel
{
    public required string Name { get; init; }
    public required string UnderlyingMemberName { get; init; }
    public required TypeId DeclaringType { get; init; }
    public required TypeId? DeclaringInterfaceType { get; init; }
    public required TypeId PropertyType { get; init; }
    
    public required bool EmitGetter { get; init; }
    public required bool EmitSetter { get; init; }
    
    public required bool IsField { get; init; }

    public required bool IsGetterNonNullableReferenceType { get; init; }
    public required bool IsSetterNonNullableReferenceType { get; init; }
}
