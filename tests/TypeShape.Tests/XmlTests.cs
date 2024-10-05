using System.Collections.Immutable;
using System.Numerics;
using System.Xml;
using TypeShape.Examples.XmlSerializer;
using Xunit;

namespace TypeShape.Tests;

public abstract class XmlTests(IProviderUnderTest providerUnderTest)
{
    private static readonly XmlWriterSettings s_writerSettings = new()
    {
        ConformanceLevel = ConformanceLevel.Fragment,
        Indent = false
    };

    [Theory]
    [MemberData(nameof(GetValuesAndExpectedEncoding))]
    public void ReturnsExpectedEncoding<T>(TestCase<T> testCase)
    {
        XmlConverter<T> converter = GetConverterUnderTest<T>(testCase);

        string xml = converter.Serialize(testCase.Value, s_writerSettings);
        Assert.Equal(testCase.ExpectedEncoding, xml);
    }

    [Theory]
    [MemberData(nameof(GetValuesAndExpectedEncoding))]
    public void ExpectedEncodingDeserializedToValue<T>(TestCase<T> testCase)
    {
        XmlConverter<T> converter = GetConverterUnderTest<T>(testCase);

        T? result = converter.Deserialize(testCase.ExpectedEncoding!);
        if (testCase.IsEquatable)
        {
            Assert.Equal(testCase.Value, result);
        }
        else
        {
            Assert.Equal(testCase.ExpectedEncoding, converter.Serialize(result, s_writerSettings));
        }
    }

    public static IEnumerable<object?[]> GetValuesAndExpectedEncoding()
    {
        SourceGenProvider p = SourceGenProvider.Default;
        yield return [TestCase.Create(p, (object?)null, """<root nil="true" />""")];
        yield return [TestCase.Create(p, false, "<root>false</root>")];
        yield return [TestCase.Create(p, true, "<root>true</root>")];
        yield return [TestCase.Create(p, 42, "<root>42</root>")];
        yield return [TestCase.Create(p, -7001, "<root>-7001</root>")];
        yield return [TestCase.Create(p, (byte)255, "<root>255</root>")];
        yield return [TestCase.Create(p, int.MaxValue, "<root>2147483647</root>")];
        yield return [TestCase.Create(p, int.MinValue, "<root>-2147483648</root>")];
        yield return [TestCase.Create(p, long.MaxValue, "<root>9223372036854775807</root>")];
        yield return [TestCase.Create(p, long.MinValue, "<root>-9223372036854775808</root>")];
        yield return [TestCase.Create(p, Int128.MaxValue, "<root>170141183460469231731687303715884105727</root>")];
        yield return [TestCase.Create(p, (BigInteger)Int128.MaxValue, "<root>170141183460469231731687303715884105727</root>")];
        yield return [TestCase.Create(p, (Half)1, "<root>1</root>")];
        yield return [TestCase.Create(p, (float)0.2, "<root>0.2</root>")];
        yield return [TestCase.Create(p, decimal.MaxValue, "<root>79228162514264337593543950335</root>")];
        yield return [TestCase.Create(p, (int[])[1, 2, 3], "<root><element>1</element><element>2</element><element>3</element></root>")];
        yield return [TestCase.Create(p, (List<int>)[1, 2, 3], "<root><element>1</element><element>2</element><element>3</element></root>")];
        yield return [TestCase.Create(p, new Dictionary<string, string> { ["key1"] = "value", ["key2"] = "value" }, "<root><entry><key>key1</key><value>value</value></entry><entry><key>key2</key><value>value</value></entry></root>")];
        yield return [TestCase.Create(p, ImmutableSortedDictionary.CreateRange<string, string>([new("key1", "value"), new("key2", "value")]), "<root><entry><key>key1</key><value>value</value></entry><entry><key>key2</key><value>value</value></entry></root>")];
        yield return [TestCase.Create(p, (byte[])[1, 2, 3], "<root>AQID</root>")];
        yield return [TestCase.Create(p, 'c', "<root>c</root>")];
        yield return [TestCase.Create(p, (string?)null, """<root nil="true" />""")];
        yield return [TestCase.Create(p, "", "<root></root>")];
        yield return [TestCase.Create(p, "Hello, World!", "<root>Hello, World!</root>")];
        yield return [TestCase.Create(p, Guid.Empty, "<root>00000000-0000-0000-0000-000000000000</root>")];
        yield return [TestCase.Create(p, TimeSpan.MinValue, "<root>-10675199.02:48:05.4775808</root>")];
        yield return [TestCase.Create(p, DateTimeOffset.MinValue, "<root>0001-01-01T00:00:00Z</root>")];
        yield return [TestCase.Create(p, DateOnly.MaxValue, "<root>9999-12-31</root>")];
        yield return [TestCase.Create(p, TimeOnly.MaxValue, "<root>23:59:59.9999999</root>")];
        yield return [TestCase.Create(new SimpleRecord(value: 42), "<root><value>42</value></root>")];
        yield return [TestCase.Create(new BaseClass { X = 42 }, "<root><X>42</X></root>")];
    }

    [Theory]
    [MemberData(nameof(TestTypes.GetTestCases), MemberType = typeof(TestTypes))]
    public void Roundtrip_Value<T>(TestCase<T> testCase)
    {
        XmlConverter<T> converter = GetConverterUnderTest<T>(testCase);

        string xmlEncoding = converter.Serialize(testCase.Value);

        if (!testCase.HasConstructors(providerUnderTest))
        {
            Assert.Throws<NotSupportedException>(() => converter.Deserialize(xmlEncoding));
        }
        else
        {
            T? deserializedValue = converter.Deserialize(xmlEncoding);

            if (testCase.IsLossyRoundtrip)
            {
                return;
            }

            if (testCase.IsEquatable)
            {
                Assert.Equal(testCase.Value, deserializedValue);
            }
            else
            {
                if (testCase.IsStack)
                {
                    deserializedValue = converter.Deserialize(converter.Serialize(deserializedValue));
                }

                Assert.Equal(xmlEncoding, converter.Serialize(deserializedValue));
            }
        }
    }

    private XmlConverter<T> GetConverterUnderTest<T>(TestCase<T> testCase) =>
        XmlSerializer.CreateConverter<T>(testCase.GetShape(providerUnderTest));
}

public sealed class XmlTests_Reflection() : XmlTests(RefectionProviderUnderTest.Default);
public sealed class XmlTests_ReflectionEmit() : XmlTests(RefectionProviderUnderTest.NoEmit);
public sealed class XmlTests_SourceGen() : XmlTests(SourceGenProviderUnderTest.Default);