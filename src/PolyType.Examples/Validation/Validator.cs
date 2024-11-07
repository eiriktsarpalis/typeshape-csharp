using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using PolyType.Abstractions;

namespace PolyType.Examples.Validation;

/// <summary>
/// Delegate containing a recursive validator walking the object graph for validation attributes.
/// </summary>
public delegate void Validator<in T>(T? value, List<string> path, ref List<string>? errors);

/// <summary>Provides an object validator for .NET types built on top of PolyType.</summary>
public static partial class Validator
{
    /// <summary>
    /// Builds a validator delegate using a type shape as input.
    /// </summary>
    public static Validator<T> Create<T>(ITypeShape<T> type) =>
        new Builder().BuildValidator(type) ?? Builder.CreateNullValidator<T>();

    /// <summary>
    /// Builds a validator delegate using a shape provider as input.
    /// </summary>
    public static Validator<T> Create<T>(ITypeShapeProvider shapeProvider) =>
        Create(shapeProvider.Resolve<T>());

    /// <summary>
    /// Runs validation against the provided value.
    /// </summary>
    public static bool TryValidate<T>(this Validator<T> validator, T? value, [NotNullWhen(false)] out List<string>? errors)
    {
        errors = null;
        var path = new List<string>();
        validator(value, path, ref errors);
        return errors is null;
    }

    /// <summary>
    /// Runs validation against the provided value.
    /// </summary>
    public static void Validate<T>(this Validator<T> validator, T? value)
    {
        if (!validator.TryValidate(value, out List<string>? errors))
        {
            throw new ValidationException($"Found validation errors:{Environment.NewLine}{string.Join(Environment.NewLine, errors)}");
        }
    }

    /// <summary>
    /// Runs validation against the provided value.
    /// </summary>
    public static bool TryValidate<T>(this T? value, [NotNullWhen(false)] out List<string>? errors) where T : IShapeable<T>
        => ValidatorCache<T, T>.Value.TryValidate(value, out errors);

    /// <summary>
    /// Runs validation against the provided value.
    /// </summary>
    public static void Validate<T>(T? value) where T : IShapeable<T>
        => ValidatorCache<T, T>.Value.Validate(value);

    /// <summary>
    /// Runs validation against the provided value.
    /// </summary>
    public static bool TryValidate<T, TProvider>(T? value, [NotNullWhen(false)] out List<string>? errors) where TProvider : IShapeable<T>
        => ValidatorCache<T, TProvider>.Value.TryValidate(value, out errors);

    /// <summary>
    /// Runs validation against the provided value.
    /// </summary>
    public static void Validate<T, TProvider>(T? value) where TProvider : IShapeable<T>
        => ValidatorCache<T, TProvider>.Value.Validate(value);

    private static class ValidatorCache<T, TProvider> where TProvider : IShapeable<T>
    {
        public static Validator<T> Value => s_value ??= Create(TProvider.GetShape());
        private static Validator<T>? s_value;
    }
}