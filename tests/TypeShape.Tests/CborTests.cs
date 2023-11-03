using System.Numerics;
using TypeShape.Applications.CborSerializer;
using TypeShape.ReflectionProvider;
using Xunit;

namespace TypeShape.Tests;

public abstract class CborTests
{
    protected abstract ITypeShapeProvider Provider { get; }

    public bool IsReflectionProvider => Provider is ReflectionTypeShapeProvider;

    [Theory]
    [MemberData(nameof(GetValuesAndExpectedEncoding))]
    public void ReturnsExpectedEncoding<T>(T value, string expectedHexEncoding)
    {
        CborConverter<T> converter = GetConverterUnderTest<T>();

        string hexEncoding = converter.EncodeToHex(value);
        Assert.Equal(expectedHexEncoding, hexEncoding);
    }

    [Theory]
    [MemberData(nameof(GetValuesAndExpectedEncoding))]
    public void ExpectedEncodingDeserializedToValue<T>(T value, string expectedHexEncoding)
    {
        CborConverter<T> converter = GetConverterUnderTest<T>();

        T? result = converter.DecodeFromHex(expectedHexEncoding);
        if (value is IEquatable<T>)
        {
            Assert.Equal(value, result);
        }
        else
        {
            Assert.Equal(expectedHexEncoding, converter.EncodeToHex(result));
        }
    }

    public static IEnumerable<object?[]> GetValuesAndExpectedEncoding()
    {
        yield return Wrap<object?>(null, "F6");
        yield return Wrap(false, "F4");
        yield return Wrap(true, "F5");
        yield return Wrap(42, "182A");
        yield return Wrap(-7001, "391B58");
        yield return Wrap((byte)255, "18FF");
        yield return Wrap(int.MaxValue, "1A7FFFFFFF");
        yield return Wrap(int.MinValue, "3A7FFFFFFF");
        yield return Wrap(long.MaxValue, "1B7FFFFFFFFFFFFFFF");
        yield return Wrap(long.MinValue, "3B7FFFFFFFFFFFFFFF");
        yield return Wrap(Int128.MaxValue, "C2507FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF");
        yield return Wrap((BigInteger)Int128.MaxValue, "C2507FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF");
        yield return Wrap((Half)3.14, "F94248");
        yield return Wrap((float)3.1415926, "FA40490FDA");
        yield return Wrap(decimal.MaxValue, "C48200C24CFFFFFFFFFFFFFFFFFFFFFFFF");
        yield return Wrap<byte[]>([1, 2, 3], "43010203");
        yield return Wrap('c', "6163");
        yield return Wrap("Hello, World!", "6D48656C6C6F2C20576F726C6421");
        yield return Wrap(Guid.Empty, "D903EA5000000000000000000000000000000000");
        yield return Wrap(DateTime.MaxValue, "C0781C393939392D31322D33315432333A35393A35392E393939393939395A");
        yield return Wrap(TimeSpan.MinValue, "D825FBC26AD7F29ABCAF48");
        yield return Wrap(DateTimeOffset.MinValue, "C074303030312D30312D30315430303A30303A30305A");
        yield return Wrap(DateOnly.MaxValue, "C074393939392D31322D33315430303A30303A30305A");
        yield return Wrap(TimeOnly.MaxValue, "D825FB40F517FFFFFFE528");
        yield return Wrap<int[]>([1, 2, 3], "83010203");
        yield return Wrap<int[][]>([[1, 0, 0], [0, 1, 0], [0, 0, 1]], "83830100008300010083000001");
        yield return Wrap(new Dictionary<string, int> { ["key0"] = 0, ["key1"] = 1 }, "A2646B65793000646B65793101");
        yield return Wrap(new SimpleRecord(42), "A16576616C7565182A");
        yield return Wrap((42, "str"), "A2654974656D31182A654974656D3263737472");

        static object?[] Wrap<T>(T value, string expectedHexEncoding) => [value, expectedHexEncoding];
    }

    [Theory]
    [MemberData(nameof(TestTypes.GetTestCases), MemberType = typeof(TestTypes))]
    public void Roundtrip_Value<T>(TestCase<T> testCase)
    {
        CborConverter<T> converter = GetConverterUnderTest<T>();

        string cborHex = converter.EncodeToHex(testCase.Value);

        if (!testCase.HasConstructors)
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

    protected CborConverter<T> GetConverterUnderTest<T>()
    {
        ITypeShape<T>? shape = Provider.GetShape<T>();
        Assert.NotNull(shape);
        return CborSerializer.CreateConverter(shape);
    }
}

public sealed class CborTests_Reflection : CborTests
{
    protected override ITypeShapeProvider Provider { get; } = new ReflectionTypeShapeProvider(useReflectionEmit: false);
}

public sealed class CborTests_ReflectionEmit : CborTests
{
    protected override ITypeShapeProvider Provider { get; } = new ReflectionTypeShapeProvider(useReflectionEmit: true);
}

public sealed class CborTests_SourceGen : CborTests
{
    protected override ITypeShapeProvider Provider { get; } = SourceGenTypeShapeProvider.Default;
}
