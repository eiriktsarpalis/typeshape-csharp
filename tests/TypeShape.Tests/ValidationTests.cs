using TypeShape.Applications.Validation;
using TypeShape.ReflectionProvider;
using Xunit;

namespace TypeShape.Tests;

public abstract class ValidationTests
{
    protected abstract ITypeShapeProvider Provider { get; }

    [Theory]
    [MemberData(nameof(GetValidatorScenaria))]
    public void SimpleValidationScenaria<T>(T value, List<string>? expectedErrors)
    {
        Validator<T> validator = GetValidatorUnderTest<T>();

        bool expectedResult = expectedErrors is null;
        bool result = validator.TryValidate(value, out List<string>? errors);
        
        Assert.Equal(expectedResult, result);
        Assert.Equal(expectedErrors, errors);
    }

    [Theory]
    [MemberData(nameof(GetValidatorScenaria))]
    public void PocoValidationScenaria<T>(T value, List<string>? expectedErrors)
    {
        Validator<GenericRecord<T>> validator = GetValidatorUnderTest<GenericRecord<T>>();
        GenericRecord<T> record = new GenericRecord<T>(value);

        expectedErrors = expectedErrors?.Select(error => error.Replace("Validation error in $.", "Validation error in $.value.")).ToList();
        bool expectedResult = expectedErrors is null;

        bool result = validator.TryValidate(record, out List<string>? errors);

        Assert.Equal(expectedResult, result);
        Assert.Equal(expectedErrors, errors);
    }

    [Theory]
    [MemberData(nameof(GetValidatorScenaria))]
    public void ListValidationScenaria<T>(T value, List<string>? expectedErrors)
    {
        Validator<List<T>> validator = GetValidatorUnderTest<List<T>>();
        List<T> list = new List<T> { value };

        expectedErrors = expectedErrors?.Select(error => error.Replace("Validation error in $.", "Validation error in $.[0].")).ToList();
        bool expectedResult = expectedErrors is null;

        bool result = validator.TryValidate(list, out List<string>? errors);

        Assert.Equal(expectedResult, result);
        Assert.Equal(expectedErrors, errors);
    }

    [Theory]
    [MemberData(nameof(GetValidatorScenaria))]
    public void DictionaryValidationScenaria<T>(T value, List<string>? expectedErrors)
    {
        Validator<Dictionary<string, T>> validator = GetValidatorUnderTest<Dictionary<string, T>>();
        Dictionary<string, T> dict = new Dictionary<string, T> { ["key"] = value };

        expectedErrors = expectedErrors?.Select(error => error.Replace("Validation error in $.", "Validation error in $.key.")).ToList();
        bool expectedResult = expectedErrors is null;

        bool result = validator.TryValidate(dict, out List<string>? errors);

        Assert.Equal(expectedResult, result);
        Assert.Equal(expectedErrors, errors);
    }

    [Theory]
    [MemberData(nameof(TestTypes.GetTestCases), MemberType = typeof(TestTypes))]
    public void TypeWithoutAttributeAnnotations_PassesValidation<T>(TestCase<T> testCase)
    {
        Validator<T> validator = GetValidatorUnderTest<T>();

        bool result = validator.TryValidate(testCase.Value, out List<string>? errors);

        Assert.True(result);
        Assert.Null(errors);
    }

    public static IEnumerable<object?[]> GetValidatorScenaria()
    {
        var validModel = new BindingModel
        {
            Id = "id",
            Components = new() { "1", "2", "3" },
            Sample = 0.517,
            PhoneNumber = "+447777777777",
        };

        yield return Create(validModel);
        yield return Create(validModel with { Id = null }, new() { "Validation error in $.Id: value is null or the empty string." });
        yield return Create(validModel with { Components = new() { "1" } }, new() { "Validation error in $.Components: collection has less than 2 or more than 5 elements." });
        yield return Create(validModel with { Components = new() { "1", "2", "3", "4", "5", "6" } }, new() { "Validation error in $.Components: collection has less than 2 or more than 5 elements." });
        yield return Create(validModel with { Sample = -1 }, new() { "Validation error in $.Sample: value is either less than 0 or larger than 1." });
        yield return Create(validModel with { Sample = 5 }, new() { "Validation error in $.Sample: value is either less than 0 or larger than 1." });
        yield return Create(validModel with { PhoneNumber = "NaN" }, new() { @"Validation error in $.PhoneNumber: value does not match regex pattern '^\+?[0-9]{7,14}$'." });

        yield return Create(new BindingModel
        {
            Id = null,
            Components = new() { "1" },
            Sample = 1.1,
            PhoneNumber = "NaN"
        },
        expectedErrors: new()
        {
            "Validation error in $.Id: value is null or the empty string.",
            "Validation error in $.Components: collection has less than 2 or more than 5 elements.",
            "Validation error in $.Sample: value is either less than 0 or larger than 1.",
            @"Validation error in $.PhoneNumber: value does not match regex pattern '^\+?[0-9]{7,14}$'."
        });

        static object?[] Create<T>(T value, List<string>? expectedErrors = null) => [value, expectedErrors];
    }

    private Validator<T> GetValidatorUnderTest<T>()
    {
        ITypeShape<T>? shape = Provider.GetShape<T>();
        Assert.NotNull(shape);
        return Validator.Create(shape);
    }

    public record BindingModel
    {
        [Required]
        public string? Id { get; set; }

        [Length(Min = 2, Max = 5)]
        public List<string>? Components { get; set; }

        [Range<double>(Min = 0, Max = 1)]
        public double Sample { get; set; }

        [RegularExpression(Pattern = @"^\+?[0-9]{7,14}$")]
        public string? PhoneNumber { get; set; }
    }
}

public class ValidationTests_Reflection : ValidationTests
{
    protected override ITypeShapeProvider Provider { get; } = new ReflectionTypeShapeProvider(useReflectionEmit: false);
}

public class ValidationTests_ReflectionEmit : ValidationTests
{
    protected override ITypeShapeProvider Provider { get; } = new ReflectionTypeShapeProvider(useReflectionEmit: true);
}

public class ValidationTests_SourceGen : ValidationTests
{
    protected override ITypeShapeProvider Provider { get; } = SourceGenTypeShapeProvider.Default;
}