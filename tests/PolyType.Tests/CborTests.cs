using System.Numerics;
using PolyType.Examples.CborSerializer;
using Xunit;

namespace PolyType.Tests;

public abstract partial class CborTests(ProviderUnderTest providerUnderTest)
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
        ITypeShapeProvider p = Witness.ShapeProvider;
        yield return [TestCase.Create((object)null!, p), "F6"];
        yield return [TestCase.Create(false, p), "F4"];
        yield return [TestCase.Create(true, p), "F5"];
        yield return [TestCase.Create(42, p), "182A"];
        yield return [TestCase.Create(-7001, p), "391B58"];
        yield return [TestCase.Create((byte)255, p), "18FF"];
        yield return [TestCase.Create(int.MaxValue, p), "1A7FFFFFFF"];
        yield return [TestCase.Create(int.MinValue, p), "3A7FFFFFFF"];
        yield return [TestCase.Create(long.MaxValue, p), "1B7FFFFFFFFFFFFFFF"];
        yield return [TestCase.Create(long.MinValue, p), "3B7FFFFFFFFFFFFFFF"];
        yield return [TestCase.Create((BigInteger)long.MaxValue, p), "C2487FFFFFFFFFFFFFFF"];
        yield return [TestCase.Create((float)3.1415926, p), "FA40490FDA"];
        yield return [TestCase.Create(decimal.MaxValue, p), "C48200C24CFFFFFFFFFFFFFFFFFFFFFFFF"];
        yield return [TestCase.Create((byte[])[1, 2, 3], p), "43010203"];
        yield return [TestCase.Create('c', p), "6163"];
        yield return [TestCase.Create("Hello, World!", p), "6D48656C6C6F2C20576F726C6421"];
        yield return [TestCase.Create(Guid.Empty, p), "D903EA5000000000000000000000000000000000"];
        yield return [TestCase.Create(TimeSpan.MinValue, p), "D825FBC26AD7F29ABCAF48"];
        yield return [TestCase.Create(DateTimeOffset.MinValue, p), "C074303030312D30312D30315430303A30303A30305A"];
#if NET
        yield return [TestCase.Create(Int128.MaxValue, p), "C2507FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF"];
        yield return [TestCase.Create((Half)3.14, p), "F94248"];
        yield return [TestCase.Create(DateOnly.MaxValue, p), "C074393939392D31322D33315430303A30303A30305A"];
        yield return [TestCase.Create(TimeOnly.MaxValue, p), "D825FB40F517FFFFFFE528"];
#endif
        yield return [TestCase.Create((int[])[1, 2, 3], p), "83010203"];
        yield return [TestCase.Create((int[][])[[1, 0, 0], [0, 1, 0], [0, 0, 1]], p), "83830100008300010083000001"];
        yield return [TestCase.Create(new Dictionary<string, int> { ["key0"] = 0, ["key1"] = 1 }, p), "A2646B65793000646B65793101"];
        yield return [TestCase.Create(new SimpleRecord(42), p), "A16576616C7565182A"];
        yield return [TestCase.Create((42, "str"), p), "A2654974656D31182A654974656D3263737472"];
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

public sealed class CborTests_Reflection() : CborTests(RefectionProviderUnderTest.NoEmit);
public sealed class CborTests_ReflectionEmit() : CborTests(RefectionProviderUnderTest.Emit);
public sealed class CborTests_SourceGen() : CborTests(SourceGenProviderUnderTest.Default);
