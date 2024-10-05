using System.Collections;
using System.Text.RegularExpressions;

namespace TypeShape.Examples.Validation;

/// <summary>
/// Defines an abstract validation attribute in the style of <see cref="System.ComponentModel.DataAnnotations.ValidationAttribute"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true, Inherited = true)]
public abstract class ValidationAttribute : Attribute
{
    /// <summary>The error message to surface on validation error.</summary>
    protected internal abstract string ErrorMessage { get; }

    /// <summary>Creates a validation predicate for a given member type.</summary>
    public abstract Predicate<TMemberType>? CreateValidationPredicate<TMemberType>();
}

/// <summary>Defines a required validation attribute in the style of <see cref="System.ComponentModel.DataAnnotations.RequiredAttribute"/>.</summary>
public class RequiredAttribute : ValidationAttribute
{
    /// <inheritdoc/>
    protected internal override string ErrorMessage => "value is null or empty.";

    /// <inheritdoc/>
    public override Predicate<TMemberType>? CreateValidationPredicate<TMemberType>()
        => static value => value is not (null or "");
}

/// <summary>Defines a length validation attribute in the style of <see cref="System.ComponentModel.DataAnnotations.LengthAttribute"/>.</summary>
public class LengthAttribute : ValidationAttribute
{
    /// <inheritdoc/>
    public required int Min { get; set; }
    /// <inheritdoc/>
    public required int Max { get; set; }
    /// <inheritdoc/>
    protected internal override string ErrorMessage => $"contains less than {Min} or more than {Max} elements.";
    /// <inheritdoc/>
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

/// <summary>Defines a range validation attribute in the style of <see cref="System.ComponentModel.DataAnnotations.RangeAttribute"/>.</summary>
public class RangeAttribute<T> : ValidationAttribute
{
    /// <inheritdoc/>
    public required T Min { get; set; }
    /// <inheritdoc/>
    public required T Max { get; set; }
    /// <inheritdoc/>
    public IComparer<T>? Comparer { get; set; }
    /// <inheritdoc/>
    protected internal override string ErrorMessage => $"value is either less than {Min} or greater than {Max}.";
    /// <inheritdoc/>
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

/// <summary>Defines a regex validation attribute in the style of <see cref="System.ComponentModel.DataAnnotations.RegularExpressionAttribute"/>.</summary>
public class RegularExpressionAttribute : ValidationAttribute
{
    /// <summary>The regex pattern to be used by the validator.</summary>
    public required string Pattern { get; set; }
    /// <inheritdoc/>
    protected internal override string ErrorMessage => $"value does not match regex pattern '{Pattern}'.";
    /// <inheritdoc/>
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