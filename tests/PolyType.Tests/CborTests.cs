using System.Numerics;
using PolyType.Examples.CborSerializer;
using Xunit;

namespace PolyType.Tests;

public abstract partial class CborTests(IProviderUnderTest providerUnderTest)
{
    [Theory]
    [MemberData(nameof(GetValuesAndExpectedEncoding))]
    public void ReturnsExpectedEncoding<T>(TestCase<T> testCase, string expectedEncoding)
    {
        CborConverter<T> converter = GetConverterUnderTest(testCase);

        string hexEncoding = converter.EncodeToHex(testCase.Value);
        Assert.Equal(expectedEncoding, hexEncoding);
    }

    [Theory]
    [MemberData(nameof(GetValuesAndExpectedEncoding))]
    public void ExpectedEncodingDeserializedToValue<T>(TestCase<T> testCase, string expectedEncoding)
    {
        CborConverter<T> converter = GetConverterUnderTest(testCase);

        T? result = converter.DecodeFromHex(expectedEncoding);
        if (testCase.IsEquatable)
        {
            Assert.Equal(testCase.Value, result);
        }
        else
        {
            Assert.Equal(expectedEncoding, converter.EncodeToHex(result));
        }
    }

    public static IEnumerable<object?[]> GetValuesAndExpectedEncoding()
    {
        SourceGenProvider p = SourceGenProvider.Default;
        yield return [TestCase.Create(p, (object)null!), "F6"];
        yield return [TestCase.Create(p, false), "F4"];
        yield return [TestCase.Create(p, true), "F5"];
        yield return [TestCase.Create(p, 42), "182A"];
        yield return [TestCase.Create(p, -7001), "391B58"];
        yield return [TestCase.Create(p, (byte)255), "18FF"];
        yield return [TestCase.Create(p, int.MaxValue), "1A7FFFFFFF"];
        yield return [TestCase.Create(p, int.MinValue), "3A7FFFFFFF"];
        yield return [TestCase.Create(p, long.MaxValue), "1B7FFFFFFFFFFFFFFF"];
        yield return [TestCase.Create(p, long.MinValue), "3B7FFFFFFFFFFFFFFF"];
        yield return [TestCase.Create(p, Int128.MaxValue), "C2507FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF"];
        yield return [TestCase.Create(p, (BigInteger)Int128.MaxValue), "C2507FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF"];
        yield return [TestCase.Create(p, (Half)3.14), "F94248"];
        yield return [TestCase.Create(p, (float)3.1415926), "FA40490FDA"];
        yield return [TestCase.Create(p, decimal.MaxValue), "C48200C24CFFFFFFFFFFFFFFFFFFFFFFFF"];
        yield return [TestCase.Create(p, (byte[])[1, 2, 3]), "43010203"];
        yield return [TestCase.Create(p, 'c'), "6163"];
        yield return [TestCase.Create(p, "Hello, World!"), "6D48656C6C6F2C20576F726C6421"];
        yield return [TestCase.Create(p, Guid.Empty), "D903EA5000000000000000000000000000000000"];
        yield return [TestCase.Create(p, TimeSpan.MinValue), "D825FBC26AD7F29ABCAF48"];
        yield return [TestCase.Create(p, DateTimeOffset.MinValue), "C074303030312D30312D30315430303A30303A30305A"];
        yield return [TestCase.Create(p, DateOnly.MaxValue), "C074393939392D31322D33315430303A30303A30305A"];
        yield return [TestCase.Create(p, TimeOnly.MaxValue), "D825FB40F517FFFFFFE528"];
        yield return [TestCase.Create(p, (int[])[1, 2, 3]), "83010203"];
        yield return [TestCase.Create(p, (int[][])[[1, 0, 0], [0, 1, 0], [0, 0, 1]]), "83830100008300010083000001"];
        yield return [TestCase.Create(p, new Dictionary<string, int> { ["key0"] = 0, ["key1"] = 1 }), "A2646B65793000646B65793101"];
        yield return [TestCase.Create(new SimpleRecord(42)), "A16576616C7565182A"];
        yield return [TestCase.Create(p, (42, "str")), "A2654974656D31182A654974656D3263737472"];
    }

    [Theory]
    [MemberData(nameof(TestTypes.GetTestCases), MemberType = typeof(TestTypes))]
    public void Roundtrip_Value<T>(TestCase<T> testCase)
    {
        CborConverter<T> converter = GetConverterUnderTest(testCase);

        string cborHex = converter.EncodeToHex(testCase.Value);

        if (!providerUnderTest.HasConstructor(testCase))
        {
            Assert.Throws<NotSupportedException>(() => converter.DecodeFromHex(cborHex));
        }
        else
        {
            T? deserializedValue = converter.DecodeFromHex(cborHex);

            if (testCase.IsEquatable)
            {
                Assert.Equal(testCase.Value, deserializedValue);
            }
            else
            {
                if (testCase.IsStack)
                {
                    deserializedValue = converter.DecodeFromHex(converter.EncodeToHex(deserializedValue));
                }

                Assert.Equal(cborHex, converter.EncodeToHex(deserializedValue));
            }
        }
    }

    private CborConverter<T> GetConverterUnderTest<T>(TestCase<T> testCase) =>
        CborSerializer.CreateConverter(providerUnderTest.ResolveShape(testCase));
}

public sealed class CborTests_Reflection() : CborTests(RefectionProviderUnderTest.Default);
public sealed class CborTests_ReflectionEmit() : CborTests(RefectionProviderUnderTest.NoEmit);
public sealed class CborTests_SourceGen() : CborTests(SourceGenProviderUnderTest.Default);
