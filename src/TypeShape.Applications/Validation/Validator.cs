using System.Diagnostics.CodeAnalysis;

namespace TypeShape.Applications.Validation;

/// <summary>
/// Delegate containing a recursive validator walking the object graph for validation attributes.
/// </summary>
public delegate void Validator<in T>(T value, List<string> path, ref List<string>? errors);

public static partial class Validator
{
    /// <summary>
    /// Builds a validator delegate using a type shape as input.
    /// </summary>
    public static Validator<T> Create<T>(IType<T> type)
    {
        var visitor = new Visitor();
        return (Validator<T>)type.Accept(visitor, null)!;
    }

    /// <summary>
    /// Runs validation against the provided value.
    /// </summary>
    public static bool TryValidate<T>(this Validator<T> validator, T value, [NotNullWhen(false)] out List<string>? errors)
    {
        errors = null;
        var path = new List<string>();
        validator(value, path, ref errors);
        return errors is null;
    }
}