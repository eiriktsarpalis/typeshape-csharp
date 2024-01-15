using Microsoft.CodeAnalysis;
using TypeShape.Roslyn.Helpers;

namespace TypeShape.Roslyn;

/// <summary>
/// Descriptor for diagnostic instances using structural equality comparison.
/// Provides a work-around for https://github.com/dotnet/roslyn/issues/68291.
/// </summary>
public readonly struct EquatableDiagnostic(
    DiagnosticDescriptor descriptor,
    Location? location,
    object?[] messageArgs) : IEquatable<EquatableDiagnostic>
{
    public DiagnosticDescriptor Descriptor { get; } = descriptor;
    public object?[] MessageArgs { get; } = messageArgs;
    public Location? Location { get; } = location?.GetLocationTrimmed();

    public Diagnostic CreateDiagnostic()
        => Diagnostic.Create(Descriptor, Location, MessageArgs);

    public override readonly bool Equals(object? obj) => obj is EquatableDiagnostic info && Equals(info);
    public readonly bool Equals(EquatableDiagnostic other)
    {
        return Descriptor.Equals(other.Descriptor) &&
            MessageArgs.SequenceEqual(other.MessageArgs) &&
            Location == other.Location;
    }

    public override readonly int GetHashCode()
    {
        int hashCode = Descriptor.GetHashCode();
        foreach (object? messageArg in MessageArgs)
        {
            hashCode = HashHelpers.Combine(hashCode, messageArg?.GetHashCode() ?? 0);
        }

        hashCode = HashHelpers.Combine(hashCode, Location?.GetHashCode() ?? 0);
        return hashCode;
    }

    public static bool operator ==(EquatableDiagnostic left, EquatableDiagnostic right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(EquatableDiagnostic left, EquatableDiagnostic right)
    {
        return !left.Equals(right);
    }
}