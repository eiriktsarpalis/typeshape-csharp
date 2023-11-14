using System.CodeDom.Compiler;
using System.Collections.Immutable;
using System.Numerics;
using System.Xml;
using TypeShape.Applications.XmlSerializer;
using TypeShape.ReflectionProvider;
using Xunit;

namespace TypeShape.Tests;

public abstract class XmlTests
{
    protected abstract ITypeShapeProvider Provider { get; }

    public bool IsReflectionProvider => Provider is ReflectionTypeShapeProvider;

    private protected readonly static XmlWriterSettings s_writerSettings = new()
    {
        ConformanceLevel = ConformanceLevel.Fragment,
        Indent = false
    };

    [Theory]
    [MemberData(nameof(GetValuesAndExpectedEncoding))]
    public void ReturnsExpectedEncoding<T>(T value, string expectedXmlEncoding)
    {
        XmlConverter<T> converter = GetConverterUnderTest<T>();

        string xml = converter.Serialize(value, s_writerSettings);
        Assert.Equal(expectedXmlEncoding, xml);
    }

    [Theory]
    [MemberData(nameof(GetValuesAndExpectedEncoding))]
    public void ExpectedEncodingDeserializedToValue<T>(T value, string expectedXmlEncoding)
    {
        XmlConverter<T> converter = GetConverterUnderTest<T>();

        T? result = converter.Deserialize(expectedXmlEncoding);
        if (value is IEquatable<T>)
        {
            Assert.Equal(value, result);
        }
        else
        {
            Assert.Equal(expectedXmlEncoding, converter.Serialize(result, s_writerSettings));
        }
    }

    public static IEnumerable<object?[]> GetValuesAndExpectedEncoding()
    {
        yield return Wrap<object?>(null, """<root nil="true" />""");
        yield return Wrap(false, "<root>false</root>");
        yield return Wrap(true, "<root>true</root>");
        yield return Wrap(42, "<root>42</root>");
        yield return Wrap(-7001, "<root>-7001</root>");
        yield return Wrap((byte)255, "<root>255</root>");
        yield return Wrap(int.MaxValue, "<root>2147483647</root>");
        yield return Wrap(int.MinValue, "<root>-2147483648</root>");
        yield return Wrap(long.MaxValue, "<root>9223372036854775807</root>");
        yield return Wrap(long.MinValue, "<root>-9223372036854775808</root>");
        yield return Wrap(Int128.MaxValue, "<root>170141183460469231731687303715884105727</root>");
        yield return Wrap((BigInteger)Int128.MaxValue, "<root>170141183460469231731687303715884105727</root>");
        yield return Wrap((Half)1, "<root>1</root>");
        yield return Wrap((float)0.2, "<root>0.2</root>");
        yield return Wrap(decimal.MaxValue, "<root>79228162514264337593543950335</root>");
        yield return Wrap<int[]>([1, 2, 3], "<root><element>1</element><element>2</element><element>3</element></root>");
        yield return Wrap<List<int>>([1, 2, 3], "<root><element>1</element><element>2</element><element>3</element></root>");
        yield return Wrap(new Dictionary<string, string> { ["key1"] = "value", ["key2"] = "value" }, "<root><entry><key>key1</key><value>value</value></entry><entry><key>key2</key><value>value</value></entry></root>");
        yield return Wrap(ImmutableSortedDictionary.CreateRange<string, string>([new("key1", "value"), new("key2", "value")]), "<root><entry><key>key1</key><value>value</value></entry><entry><key>key2</key><value>value</value></entry></root>");
        yield return Wrap<byte[]>([1, 2, 3], "<root>AQID</root>");
        yield return Wrap('c', "<root>c</root>");
        yield return Wrap<string?>(null, """<root nil="true" />""");
        yield return Wrap("", "<root></root>");
        yield return Wrap("Hello, World!", "<root>Hello, World!</root>");
        yield return Wrap(Guid.Empty, "<root>00000000-0000-0000-0000-000000000000</root>");
        yield return Wrap(DateTime.MaxValue, "<root>9999-12-31T23:59:59.9999999Z</root>");
        yield return Wrap(TimeSpan.MinValue, "<root>-10675199.02:48:05.4775808</root>");
        yield return Wrap(DateTimeOffset.MinValue, "<root>0001-01-01T00:00:00Z</root>");
        yield return Wrap(DateOnly.MaxValue, "<root>9999-12-31</root>");
        yield return Wrap(TimeOnly.MaxValue, "<root>23:59:59.9999999</root>");
        yield return Wrap(new SimpleRecord(value: 42), "<root><value>42</value></root>");
        yield return Wrap(new BaseClass { X = 42 }, "<root><X>42</X></root>");

        static object?[] Wrap<T>(T value, string expectedHexEncoding) => [value, expectedHexEncoding];
    }

    [Theory]
    [MemberData(nameof(TestTypes.GetTestCases), MemberType = typeof(TestTypes))]
    public void Roundtrip_Value<T>(TestCase<T> testCase)
    {
        XmlConverter<T> converter = GetConverterUnderTest<T>();

        string xmlEncoding = converter.Serialize(testCase.Value);

        if (!testCase.HasConstructors)
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

    protected XmlConverter<T> GetConverterUnderTest<T>()
    {
        ITypeShape<T>? shape = Provider.GetShape<T>();
        Assert.NotNull(shape);
        return XmlSerializer.CreateConverter(shape);
    }
}

public sealed class XmlTests_Reflection : XmlTests
{
    protected override ITypeShapeProvider Provider { get; } = new ReflectionTypeShapeProvider(useReflectionEmit: false);
}

public sealed class XmlTests_ReflectionEmit : XmlTests
{
    protected override ITypeShapeProvider Provider { get; } = new ReflectionTypeShapeProvider(useReflectionEmit: true);
}

public sealed class XmlTests_SourceGen : XmlTests
{
    [Theory]
    [MemberData(nameof(TestTypes.GetTestCases), MemberType = typeof(TestTypes))]
    public void Roundtrip_TypeShapeProvider<T, TProvider>(TestCase<T, TProvider> testCase) where TProvider : ITypeShapeProvider<T>
    {
        string xmlEncoding = XmlSerializer.Serialize<T, TProvider>(testCase.Value);

        if (!testCase.HasConstructors)
        {
            Assert.Throws<NotSupportedException>(() => XmlSerializer.Deserialize<T, TProvider>(xmlEncoding));
        }
        else
        {
            T? deserializedValue = XmlSerializer.Deserialize<T, TProvider>(xmlEncoding);

            if (testCase.IsEquatable)
            {
                Assert.Equal(testCase.Value, deserializedValue);
            }
            else
            {
                if (testCase.IsStack)
                {
                    deserializedValue = XmlSerializer.Deserialize<T, TProvider>(XmlSerializer.Serialize<T, TProvider>(deserializedValue));
                }

                Assert.Equal(xmlEncoding, XmlSerializer.Serialize<T, TProvider>(deserializedValue));
            }
        }
    }

    protected override ITypeShapeProvider Provider { get; } = SourceGenProvider.Default;
}
