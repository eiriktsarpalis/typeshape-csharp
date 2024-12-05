using PolyType.Examples.Validation;
using Xunit;

namespace PolyType.Tests;

public abstract partial class ValidationTests(ProviderUnderTest providerUnderTest)
{
    [Theory]
    [MemberData(nameof(GetValidatorScenaria))]
    public void SimpleValidationScenaria<T>(TestCase<T> testCase, List<string>? expectedErrors)
    {
        Validator<T> validator = GetValidatorUnderTest(testCase);

        bool expectedResult = expectedErrors is null;
        bool result = validator.TryValidate(testCase.Value, out List<string>? errors);
        
        Assert.Equal(expectedResult, result);
        Assert.Equal(expectedErrors, errors);
    }

    [Theory]
    [MemberData(nameof(TestTypes.GetTestCases), MemberType = typeof(TestTypes))]
    public void TypeWithoutAttributeAnnotations_PassesValidation<T>(TestCase<T> testCase)
    {
        Validator<T> validator = GetValidatorUnderTest(testCase);
        bool result = validator.TryValidate(testCase.Value, out List<string>? errors);

        Assert.True(result);
        Assert.Null(errors);
    }

    public static IEnumerable<object?[]> GetValidatorScenaria()
    {
        ITypeShapeProvider provider = ModelProvider.ShapeProvider;
        var validModel = new BindingModel
        {
            Id = "id",
            Components = ["1", "2", "3"],
            Sample = 0.517,
            PhoneNumber = "+447777777777",
        };

        yield return Create(TestCase.Create(validModel, provider));
        yield return Create(TestCase.Create(validModel with { Id = null }, provider), ["$.Id: value is null or empty."]);
        yield return Create(TestCase.Create(validModel with { Components = ["1"] }, provider), ["$.Components: contains less than 2 or more than 5 elements."]);
        yield return Create(TestCase.Create(validModel with { Components = ["1", "2", "3", "4", "5", "6"] }, provider), ["$.Components: contains less than 2 or more than 5 elements."]);
        yield return Create(TestCase.Create(validModel with { Sample = -1 }, provider), ["$.Sample: value is either less than 0 or greater than 1."]);
        yield return Create(TestCase.Create(validModel with { Sample = 5 }, provider), ["$.Sample: value is either less than 0 or greater than 1."]);
        yield return Create(TestCase.Create(validModel with { PhoneNumber = "NaN" }, provider), [@"$.PhoneNumber: value does not match regex pattern '^\+?[0-9]{7,14}$'."]);

        yield return Create(TestCase.Create(new BindingModel
        {
            Id = null,
            Components = ["1"],
            Sample = 1.1,
            PhoneNumber = "NaN"
        }, provider),
        expectedErrors:
        [
            "$.Id: value is null or empty.",
            "$.Components: contains less than 2 or more than 5 elements.",
            "$.Sample: value is either less than 0 or greater than 1.",
            @"$.PhoneNumber: value does not match regex pattern '^\+?[0-9]{7,14}$'."
        ]);

        yield return Create(
            TestCase.Create(new GenericRecord<BindingModel>(validModel with { Id = null }), provider),
            expectedErrors: ["$.value.Id: value is null or empty.",]
        );

        yield return Create(
            TestCase.Create(new List<BindingModel> { validModel with { Id = null } }, provider),
            expectedErrors: ["$.[0].Id: value is null or empty.",]
        );

        yield return Create(
            TestCase.Create(new Dictionary<string, BindingModel> { ["key"] = validModel with { Id = null } }, provider),
            expectedErrors: ["$.key.Id: value is null or empty.",]
        );
        
        static object?[] Create<T>(TestCase<T> value, List<string>? expectedErrors = null) => [value, expectedErrors];
    }

    private Validator<T> GetValidatorUnderTest<T>(TestCase<T> testCase) =>
        Validator.Create(providerUnderTest.ResolveShape(testCase));

    [GenerateShape]
    public partial record BindingModel
    {
        [Required]
        public string? Id { get; set; }

        [Length(Min = 2, Max = 5)]
        public List<string>? Components { get; set; }

        [RangeDouble(Min = 0, Max = 1)]
        public double Sample { get; set; }

        [RegularExpression(Pattern = @"^\+?[0-9]{7,14}$")]
        public string? PhoneNumber { get; set; }
    }
    
    // Workaround for .NET Framework not supporting generic attributes.
    public class RangeDoubleAttribute : RangeAttribute<double>;

    [GenerateShape<GenericRecord<BindingModel>>]
    [GenerateShape<List<BindingModel>>]
    [GenerateShape<Dictionary<string, BindingModel>>]
    public partial class ModelProvider;
}

public sealed class ValidationTests_Reflection() : ValidationTests(RefectionProviderUnderTest.NoEmit);
public sealed class ValidationTests_ReflectionEmit() : ValidationTests(RefectionProviderUnderTest.Emit);
public sealed class ValidationTests_SourceGen() : ValidationTests(SourceGenProviderUnderTest.Default);