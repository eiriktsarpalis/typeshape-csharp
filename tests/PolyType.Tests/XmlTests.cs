using System.Collections.Immutable;
using System.Numerics;
using System.Xml;
using PolyType.Examples.XmlSerializer;
using Xunit;

namespace PolyType.Tests;

public abstract class XmlTests(ProviderUnderTest providerUnderTest)
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
        ITypeShapeProvider p = Witness.ShapeProvider;
        yield return [TestCase.Create((object?)null, p), """<value nil="true" />"""];
        yield return [TestCase.Create(false, p), "<value>false</value>"];
        yield return [TestCase.Create(true, p), "<value>true</value>"];
        yield return [TestCase.Create(42, p), "<value>42</value>"];
        yield return [TestCase.Create(-7001, p), "<value>-7001</value>"];
        yield return [TestCase.Create((byte)255, p), "<value>255</value>"];
        yield return [TestCase.Create(int.MaxValue, p), "<value>2147483647</value>"];
        yield return [TestCase.Create(int.MinValue, p), "<value>-2147483648</value>"];
        yield return [TestCase.Create(long.MaxValue, p), "<value>9223372036854775807</value>"];
        yield return [TestCase.Create(long.MinValue, p), "<value>-9223372036854775808</value>"];
        yield return [TestCase.Create((BigInteger)long.MaxValue, p), "<value>9223372036854775807</value>"];
        yield return [TestCase.Create((float)0.2, p), "<value>0.2</value>"];
        yield return [TestCase.Create(decimal.MaxValue, p), "<value>79228162514264337593543950335</value>"];
        yield return [TestCase.Create((int[])[1, 2, 3], p), "<value><element>1</element><element>2</element><element>3</element></value>"];
        yield return [TestCase.Create((List<int>)[1, 2, 3], p), "<value><element>1</element><element>2</element><element>3</element></value>"];
        yield return [TestCase.Create(new Dictionary<string, string> { ["key1"] = "value", ["key2"] = "value" }, p), "<value><entry><key>key1</key><value>value</value></entry><entry><key>key2</key><value>value</value></entry></value>"];
        yield return [TestCase.Create(ImmutableSortedDictionary.CreateRange<string, string>([new("key1", "value"), new("key2", "value")]), p), "<value><entry><key>key1</key><value>value</value></entry><entry><key>key2</key><value>value</value></entry></value>"];
        yield return [TestCase.Create((byte[])[1, 2, 3], p), "<value>AQID</value>"];
        yield return [TestCase.Create('c', p), "<value>c</value>"];
        yield return [TestCase.Create((string?)null, p), """<value nil="true" />"""];
        yield return [TestCase.Create("", p), "<value></value>"];
        yield return [TestCase.Create("Hello, World!", p), "<value>Hello, World!</value>"];
        yield return [TestCase.Create(Guid.Empty, p), "<value>00000000-0000-0000-0000-000000000000</value>"];
        yield return [TestCase.Create(TimeSpan.MinValue, p), "<value>-10675199.02:48:05.4775808</value>"];
        yield return [TestCase.Create(DateTimeOffset.MinValue, p), "<value>0001-01-01T00:00:00Z</value>"];
#if NET
        yield return [TestCase.Create(Int128.MaxValue, p), "<value>170141183460469231731687303715884105727</value>"];
        yield return [TestCase.Create((Half)1, p), "<value>1</value>"];
        yield return [TestCase.Create(DateOnly.MaxValue, p), "<value>9999-12-31</value>"];
        yield return [TestCase.Create(TimeOnly.MaxValue, p), "<value>23:59:59.9999999</value>"];
#endif
        yield return [TestCase.Create(new SimpleRecord(value: 42), p), "<value><value>42</value></value>"];
        yield return [TestCase.Create(new BaseClass { X = 42 }, p), "<value><X>42</X></value>"];
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
        XmlSerializer.CreateConverter(providerUnderTest.ResolveShape(testCase));
}

public sealed class XmlTests_Reflection() : XmlTests(RefectionProviderUnderTest.NoEmit);
public sealed class XmlTests_ReflectionEmit() : XmlTests(RefectionProviderUnderTest.Emit);
public sealed class XmlTests_SourceGen() : XmlTests(SourceGenProviderUnderTest.Default);