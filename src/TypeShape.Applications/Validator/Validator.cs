using System.Diagnostics.CodeAnalysis;

namespace TypeShape.Applications.Validator;

public delegate void Validator<T>
    (Func<object?, bool> isValid, T rootValue,
     List<string> path, ref List<string>? errors);

public static partial class Validator
{
    private static readonly Visitor s_Visitor = new();

    public static Validator<T> Create<T>(IType<T> type) 
        => (Validator<T>)s_Visitor.VisitType(type, null)!;

    public static bool TryValidate<T>(
        this Validator<T> validator, T value, 
        Func<object?, bool> isValid, [NotNullWhen(false)] out List<string>? errors)
    {
        errors = null;
        var path = new List<string>();
        validator(isValid, value, path, ref errors);
        return errors is null;
    }
}

