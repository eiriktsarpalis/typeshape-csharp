using PolyType.Examples.Validation;
using Xunit;

namespace PolyType.Tests;

public abstract partial class ValidationTests(IProviderUnderTest providerUnderTest)
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

        var provider = new ModelProvider();
        yield return CreateWithProvider(provider,
            value: new GenericRecord<BindingModel>(validModel with { Id = null }),
            expectedErrors: ["$.value.Id: value is null or empty.",]
        );

        yield return CreateWithProvider(provider,
            value: new List<BindingModel> { validModel with { Id = null } },
            expectedErrors: ["$.[0].Id: value is null or empty.",]
        );

        yield return CreateWithProvider(provider,
            value: new Dictionary<string, BindingModel> { ["key"] = validModel with { Id = null }},
            expectedErrors: ["$.key.Id: value is null or empty.",]
        );
        
        static object?[] Create<T>(T? value, List<string>? expectedErrors = null) where T : IShapeable<T> =>
            CreateWithProvider(value, value, expectedErrors);

        static object?[] CreateWithProvider<TProvider, T>(TProvider? provider, T? value, List<string>? expectedErrors = null) where TProvider : IShapeable<T> =>
            [TestCase.Create(provider, value), expectedErrors];
    }

    private Validator<T> GetValidatorUnderTest<T>(TestCase<T> testCase) =>
        Validator.Create<T>(providerUnderTest.ResolveShape(testCase));

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

    [GenerateShape<GenericRecord<BindingModel>>]
    [GenerateShape<List<BindingModel>>]
    [GenerateShape<Dictionary<string, BindingModel>>]
    public partial class ModelProvider;
}

public sealed class ValidationTests_Reflection() : ValidationTests(RefectionProviderUnderTest.Default);
public sealed class ValidationTests_ReflectionEmit() : ValidationTests(RefectionProviderUnderTest.NoEmit);
public sealed class ValidationTests_SourceGen() : ValidationTests(SourceGenProviderUnderTest.Default);