# TypeShape consumer test cases

This package is intended for consumption by your unit test project. It presents a bunch of types that help to exhaustively test your TypeShape visitors.

```cs
private static readonly IProviderUnderTest Provider = SourceGenProviderUnderTest.Default; // or RefectionProviderUnderTest.Default;

[Theory]
[MemberData(nameof(TestTypes.GetTestCases), MemberType = typeof(TestTypes))]
public void Roundtrip_Value<T>(TestCase<T> testCase)
{
    T value = testCase.Value;
    Type type = testCase.Type;
    ITypeShape<T> shape = testCase.GetShape(Provider);

    // Exercise your code using these values...
}
```

There are other properties on the test case that may be helpful in knowing whether you can perform certain operations on the test case with an expectation of success.
