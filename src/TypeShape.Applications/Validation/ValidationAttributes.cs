using System.Collections;
using System.Text.RegularExpressions;

namespace TypeShape.Applications.Validation;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true, Inherited = true)]
public abstract class ValidationAttribute : Attribute
{
    /// <summary>
    /// The error message to surface on validation error.
    /// </summary>
    protected internal abstract string ErrorMessage { get; }

    /// <summary>
    /// Instantiates a validation predicate for a given member type.
    /// </summary>
    public abstract Predicate<TMemberType>? CreateValidationPredicate<TMemberType>();
}

public class RequiredAttribute : ValidationAttribute
{
    protected internal override string ErrorMessage => "value is null or empty.";

    public override Predicate<TMemberType>? CreateValidationPredicate<TMemberType>()
        => static value => value is not (null or "");
}

public class LengthAttribute : ValidationAttribute
{
    public required int Min { get; set; }
    public required int Max { get; set; }

    protected internal override string ErrorMessage => $"contains less than {Min} or more than {Max} elements.";

    public override Predicate<TMemberType>? CreateValidationPredicate<TMemberType>()
    {
        if (typeof(TMemberType) == typeof(string))
        {
            return (Predicate<TMemberType>)(object)new Predicate<string>(StringPredicate);
            bool StringPredicate(string? str)
                => str != null && Min <= str.Length && str.Length <= Max;
        }

        if (typeof(ICollection).IsAssignableFrom(typeof(TMemberType)))
        {
            return collection =>
            {
                int count = collection is null ? 0 : ((ICollection)collection!).Count;
                return Min <= count && count <= Max;
            };
        }

        return null;
    }
}

public class RangeAttribute<T> : ValidationAttribute
{
    public required T Min { get; set; }
    public required T Max { get; set; }
    public IComparer<T>? Comparer { get; set; }

    protected internal override string ErrorMessage => $"value is either less than {Min} or greater than {Max}.";

    public override Predicate<TMemberType>? CreateValidationPredicate<TMemberType>()
    {
        if (typeof(T).IsAssignableFrom(typeof(TMemberType)))
        {
            var comparer = Comparer ?? Comparer<T>.Default;
            return (Predicate<TMemberType>)(object)new Predicate<T>(value => comparer.Compare(Min, value) <= 0 && comparer.Compare(value, Max) <= 0);
        }

        return null;
    }
}

public class RegularExpressionAttribute : ValidationAttribute
{
    public required string Pattern { get; set; }

    protected internal override string ErrorMessage => $"value does not match regex pattern '{Pattern}'.";

    public override Predicate<TMemberType>? CreateValidationPredicate<TMemberType>()
    {
        if (typeof(TMemberType) == typeof(string))
        {
            var regex = new Regex(Pattern, RegexOptions.Compiled, matchTimeout: TimeSpan.FromMilliseconds(100));
            return (Predicate<TMemberType>)(object)new Predicate<string>(value => value != null && regex.IsMatch(value));
        }

        return null;
    }
}