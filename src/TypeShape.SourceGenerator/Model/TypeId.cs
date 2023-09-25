using Microsoft.CodeAnalysis;

namespace TypeShape.SourceGenerator.Model;

public readonly struct TypeId : IEquatable<TypeId>
{
    public required string FullyQualifiedName { get; init; }
    public required string GeneratedPropertyName { get; init; }
    public required bool IsValueType { get; init; }
    public required SpecialType SpecialType { get; init; }

    public bool Equals(TypeId other) => FullyQualifiedName == other.FullyQualifiedName;
    public override int GetHashCode() => FullyQualifiedName.GetHashCode();
    public override bool Equals(object obj) => obj is TypeId other && Equals(other);
    public static bool operator ==(TypeId left, TypeId right) => left.Equals(right);
    public static bool operator !=(TypeId left, TypeId right) => !(left == right);
}