namespace TypeShape.SourceGenerator.Model;

public sealed record PropertyShapeModel
{
    public required string Name { get; init; }
    public required string UnderlyingMemberName { get; init; }
    public required TypeId DeclaringType { get; init; }
    public required TypeId PropertyType { get; init; }
    
    public required bool IsField { get; init; }

    public required bool EmitGetter { get; init; }
    public required bool EmitSetter { get; init; }

    public required bool IsGetterPublic { get; init; }
    public required bool IsSetterPublic { get; init; }

    public required bool IsGetterNonNullable { get; init; }
    public required bool IsSetterNonNullable { get; init; }

    public required int Order { get; init; }
}
