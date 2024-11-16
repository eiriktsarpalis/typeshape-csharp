using Microsoft.CodeAnalysis;

namespace PolyType.SourceGenerator.Model;

/// <summary>
/// Represents a cacheable type identifier that uses FQN to derive equality.
/// </summary>
public readonly struct TypeId : IEquatable<TypeId>
{
    public required string FullyQualifiedName { get; init; }
    public required bool IsValueType { get; init; }
    public required SpecialType SpecialType { get; init; }

    public bool Equals(TypeId other) => FullyQualifiedName == other.FullyQualifiedName;
    public override int GetHashCode() => FullyQualifiedName.GetHashCode();
    public override bool Equals(object obj) => obj is TypeId other && Equals(other);
    public static bool operator ==(TypeId left, TypeId right) => left.Equals(right);
    public static bool operator !=(TypeId left, TypeId right) => !(left == right);
    public override string ToString() => FullyQualifiedName;
}