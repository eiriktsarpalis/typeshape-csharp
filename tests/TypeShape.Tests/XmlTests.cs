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
    public void ReturnsExpectedEncoding<T>(TestCase<T> testCase, string expectedEncoding)
    {
        XmlConverter<T> converter = GetConverterUnderTest<T>(testCase);

        string xml = converter.Serialize(testCase.Value, s_writerSettings);
        Assert.Equal(expectedEncoding, xml);
    }

    [Theory]
    [MemberData(nameof(GetValuesAndExpectedEncoding))]
    public void ExpectedEncodingDeserializedToValue<T>(TestCase<T> testCase, string expectedEncoding)
    {
        XmlConverter<T> converter = GetConverterUnderTest<T>(testCase);

        T? result = converter.Deserialize(expectedEncoding);
        if (testCase.IsEquatable)
        {
            Assert.Equal(testCase.Value, result);
        }
        else
        {
            Assert.Equal(expectedEncoding, converter.Serialize(result, s_writerSettings));
        }
    }

    public static IEnumerable<object?[]> GetValuesAndExpectedEncoding()
    {
        SourceGenProvider p = SourceGenProvider.Default;
        yield return [TestCase.Create(p, (object?)null), """<value nil="true" />"""];
        yield return [TestCase.Create(p, false), "<value>false</value>"];
        yield return [TestCase.Create(p, true), "<value>true</value>"];
        yield return [TestCase.Create(p, 42), "<value>42</value>"];
        yield return [TestCase.Create(p, -7001), "<value>-7001</value>"];
        yield return [TestCase.Create(p, (byte)255), "<value>255</value>"];
        yield return [TestCase.Create(p, int.MaxValue), "<value>2147483647</value>"];
        yield return [TestCase.Create(p, int.MinValue), "<value>-2147483648</value>"];
        yield return [TestCase.Create(p, long.MaxValue), "<value>9223372036854775807</value>"];
        yield return [TestCase.Create(p, long.MinValue), "<value>-9223372036854775808</value>"];
        yield return [TestCase.Create(p, Int128.MaxValue), "<value>170141183460469231731687303715884105727</value>"];
        yield return [TestCase.Create(p, (BigInteger)Int128.MaxValue), "<value>170141183460469231731687303715884105727</value>"];
        yield return [TestCase.Create(p, (Half)1), "<value>1</value>"];
        yield return [TestCase.Create(p, (float)0.2), "<value>0.2</value>"];
        yield return [TestCase.Create(p, decimal.MaxValue), "<value>79228162514264337593543950335</value>"];
        yield return [TestCase.Create(p, (int[])[1, 2, 3]), "<value><element>1</element><element>2</element><element>3</element></value>"];
        yield return [TestCase.Create(p, (List<int>)[1, 2, 3]), "<value><element>1</element><element>2</element><element>3</element></value>"];
        yield return [TestCase.Create(p, new Dictionary<string, string> { ["key1"] = "value", ["key2"] = "value" }), "<value><entry><key>key1</key><value>value</value></entry><entry><key>key2</key><value>value</value></entry></value>"];
        yield return [TestCase.Create(p, ImmutableSortedDictionary.CreateRange<string, string>([new("key1", "value"), new("key2", "value")])), "<value><entry><key>key1</key><value>value</value></entry><entry><key>key2</key><value>value</value></entry></value>"];
        yield return [TestCase.Create(p, (byte[])[1, 2, 3]), "<value>AQID</value>"];
        yield return [TestCase.Create(p, 'c'), "<value>c</value>"];
        yield return [TestCase.Create(p, (string?)null), """<value nil="true" />"""];
        yield return [TestCase.Create(p, ""), "<value></value>"];
        yield return [TestCase.Create(p, "Hello, World!"), "<value>Hello, World!</value>"];
        yield return [TestCase.Create(p, Guid.Empty), "<value>00000000-0000-0000-0000-000000000000</value>"];
        yield return [TestCase.Create(p, TimeSpan.MinValue), "<value>-10675199.02:48:05.4775808</value>"];
        yield return [TestCase.Create(p, DateTimeOffset.MinValue), "<value>0001-01-01T00:00:00Z</value>"];
        yield return [TestCase.Create(p, DateOnly.MaxValue), "<value>9999-12-31</value>"];
        yield return [TestCase.Create(p, TimeOnly.MaxValue), "<value>23:59:59.9999999</value>"];
        yield return [TestCase.Create(new SimpleRecord(value: 42)), "<value><value>42</value></value>"];
        yield return [TestCase.Create(new BaseClass { X = 42 }), "<value><X>42</X></value>"];
    }

    [Theory]
    [MemberData(nameof(TestTypes.GetTestCases), MemberType = typeof(TestTypes))]
    public void Roundtrip_Value<T>(TestCase<T> testCase)
    {
        XmlConverter<T> converter = GetConverterUnderTest<T>(testCase);

        string xmlEncoding = converter.Serialize(testCase.Value);

        if (!providerUnderTest.HasConstructor(testCase))
        {
            Assert.Throws<NotSupportedException>(() => converter.Deserialize(xmlEncoding));
        }
        else
        {
            T? deserializedValue = converter.Deserialize(xmlEncoding);

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
        XmlSerializer.CreateConverter<T>(providerUnderTest.ResolveShape(testCase));
}

public sealed class XmlTests_Reflection() : XmlTests(RefectionProviderUnderTest.Default);
public sealed class XmlTests_ReflectionEmit() : XmlTests(RefectionProviderUnderTest.NoEmit);
public sealed class XmlTests_SourceGen() : XmlTests(SourceGenProviderUnderTest.Default);