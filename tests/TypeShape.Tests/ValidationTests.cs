using TypeShape.Abstractions;
using TypeShape.Applications.Validation;
using TypeShape.ReflectionProvider;
using Xunit;

namespace TypeShape.Tests;

public abstract partial class ValidationTests
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

        expectedErrors = expectedErrors?.Select(error => error.Replace("$.", "$.value.")).ToList();
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

        expectedErrors = expectedErrors?.Select(error => error.Replace("$.", "$.[0].")).ToList();
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

        expectedErrors = expectedErrors?.Select(error => error.Replace("$.", "$.key.")).ToList();
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
            Components = ["1", "2", "3"],
            Sample = 0.517,
            PhoneNumber = "+447777777777",
        };

        yield return Create(validModel);
        yield return Create(validModel with { Id = null }, ["$.Id: value is null or empty."]);
        yield return Create(validModel with { Components = ["1"] }, ["$.Components: contains less than 2 or more than 5 elements."]);
        yield return Create(validModel with { Components = ["1", "2", "3", "4", "5", "6"] }, ["$.Components: contains less than 2 or more than 5 elements."]);
        yield return Create(validModel with { Sample = -1 }, ["$.Sample: value is either less than 0 or greater than 1."]);
        yield return Create(validModel with { Sample = 5 }, ["$.Sample: value is either less than 0 or greater than 1."]);
        yield return Create(validModel with { PhoneNumber = "NaN" }, [@"$.PhoneNumber: value does not match regex pattern '^\+?[0-9]{7,14}$'."]);

        yield return Create(new BindingModel
        {
            Id = null,
            Components = ["1"],
            Sample = 1.1,
            PhoneNumber = "NaN"
        },
        expectedErrors:
        [
            "$.Id: value is null or empty.",
            "$.Components: contains less than 2 or more than 5 elements.",
            "$.Sample: value is either less than 0 or greater than 1.",
            @"$.PhoneNumber: value does not match regex pattern '^\+?[0-9]{7,14}$'."
        ]);

        static object?[] Create<T>(T value, List<string>? expectedErrors = null) => [value, expectedErrors];
    }

    private Validator<T> GetValidatorUnderTest<T>() => Validator.Create<T>(Provider);

    [GenerateShape]
    public partial record BindingModel
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
    [Theory]
    [MemberData(nameof(GetValidatorScenaria))]
    public void SelfShapeProvider_SimpleValidationScenaria<T>(T value, List<string>? expectedErrors) where T : ITypeShapeProvider<T>
    {
        bool expectedResult = expectedErrors is null;
        bool result = value.TryValidate(out List<string>? errors);

        Assert.Equal(expectedResult, result);
        Assert.Equal(expectedErrors, errors);
    }

    [Theory]
    [MemberData(nameof(TestTypes.GetTestCases), MemberType = typeof(TestTypes))]
    public void TypeWithoutAttributeAnnotations_TypeShapeProvider_PassesValidation<T, TProvider>(TestCase<T, TProvider> testCase) 
        where TProvider : ITypeShapeProvider<T>
    {
        bool result = Validator.TryValidate<T, TProvider>(testCase.Value, out List<string>? errors);

        Assert.True(result);
        Assert.Null(errors);
    }

    protected override ITypeShapeProvider Provider { get; } = SourceGenProvider.Default;
}