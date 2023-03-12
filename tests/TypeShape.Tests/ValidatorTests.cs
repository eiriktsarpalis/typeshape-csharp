using TypeShape.Applications.Validator;
using TypeShape.ReflectionProvider;
using Xunit;

namespace TypeShape.Tests;

public abstract class ValidatorTests
{
    protected abstract ITypeShapeProvider Provider { get; }

    [Theory]
    [MemberData(nameof(GetValidatorScenaria))]
    public void ValidatorScenaria<T>(T value, Func<object?, bool> isValid, List<string>? expectedErrors)
    {
        Validator<T> validator = GetValidatorUnderTest<T>();

        bool expectedResult = expectedErrors is null or { Count: 0 };
        bool result = validator.TryValidate(value, isValid, out List<string>? errors);
        
        Assert.Equal(expectedResult, result);
        Assert.Equal(expectedErrors, errors);
    }

    public static IEnumerable<object?[]> GetValidatorScenaria()
    {
        Func<object?, bool> positiveInteger = value => value is int i ? i >= 0 : true;

        yield return Scenario(1, positiveInteger);
        yield return Scenario(-1, positiveInteger, new() { "Value in path $. is not valid." });
        yield return Scenario(
            new int[] { 1, -1, 1, -1 }, 
            positiveInteger,
            new () { 
                "Value in path $.[1] is not valid." ,
                "Value in path $.[3] is not valid."
            });

        yield return Scenario(
            new Dictionary<string, SimpleRecord> { ["key"] = new SimpleRecord(-1) }, 
            positiveInteger,
            new() {
                "Value in path $.key.value is not valid." ,
            });

        yield return Scenario(new SimpleRecord(1), positiveInteger);
        yield return Scenario(new SimpleRecord(-1), positiveInteger, new() { "Value in path $.value is not valid." });
        yield return Scenario(new GenericRecord<GenericRecord<int>>(new(42)), positiveInteger);
        yield return Scenario(new GenericRecord<GenericRecord<int>>(new(-1)), positiveInteger, new() { "Value in path $.value.value is not valid." });


        static object?[] Scenario<T>(T value, Func<object?, bool> isValid, List<string>? expectedErrors = null)
            => new object?[] { value, isValid, expectedErrors };
    }

    private Validator<T> GetValidatorUnderTest<T>()
    {
        IType<T>? shape = Provider.GetShape<T>();
        Assert.NotNull(shape);
        return Validator.Create(shape);
    }
}

public class ValidatorTests_Reflection : ValidatorTests
{
    protected override ITypeShapeProvider Provider { get; } = new ReflectionTypeShapeProvider(useReflectionEmit: false);
}

public class ValidatorTests_ReflectionEmit : ValidatorTests
{
    protected override ITypeShapeProvider Provider { get; } = new ReflectionTypeShapeProvider(useReflectionEmit: true);
}

public class ValidatorTests_SourceGen : ValidatorTests
{
    protected override ITypeShapeProvider Provider { get; } = SourceGenTypeShapeProvider.Default;
}