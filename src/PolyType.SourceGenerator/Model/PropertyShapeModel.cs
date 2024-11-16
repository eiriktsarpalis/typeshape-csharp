namespace PolyType.SourceGenerator.Model;

public sealed record PropertyShapeModel
{
    public required string Name { get; init; }
    public required string UnderlyingMemberName { get; init; }
    public required TypeId DeclaringType { get; init; }
    public required TypeId PropertyType { get; init; }
    
    public required bool IsField { get; init; }
    public required bool IsInitOnly { get; init; }

    public required bool EmitGetter { get; init; }
    public required bool EmitSetter { get; init; }

    public required bool IsGetterAccessible { get; init; }
    public required bool IsSetterAccessible { get; init; }

    public required bool IsGetterPublic { get; init; }
    public required bool IsSetterPublic { get; init; }

    public required bool IsGetterNonNullable { get; init; }
    public required bool IsSetterNonNullable { get; init; }
    
    /// <summary>
    /// Whether the property type or type parameters of the
    /// property type contain nullability annotations.
    /// </summary>
    public required bool PropertyTypeContainsNullabilityAnnotations { get; init; }

    /// <summary>
    /// Determines if the property can use unsafe accessors in the generated code.
    /// </summary>
    public required bool CanUseUnsafeAccessors { get; init; }

    public required int Order { get; init; }
}
