using MessagePack;
using MessagePack.Formatters;
using Nerdbank.Streams;
using TypeShape.Examples.MsgPackSerializer;
using Xunit;
using Xunit.Abstractions;

namespace TypeShape.Tests;

public partial class MsgPackSerializerTests(ITestOutputHelper logger)
{
    private readonly IProviderUnderTest providerUnderTest = SourceGenProviderUnderTest.Default;

    [Fact]
    public void ClassWithPropertySetters() => AssertRoundtrip(new DerivedClass { X = 42, Y = 84 });

    [Fact]
    public void StructWithFields() => AssertRoundtrip(new ComplexStruct { im = 1.2, real = 3.5 });

    [Fact]
    public void SimpleRecord() => AssertRoundtrip(new RecordWithParamAndProperty("My name") { Age = 15 });

    [Fact]
    public void JustAnEnum() => AssertRoundtrip<MyEnum, Witness>(MyEnum.H);

    [Theory]
    [MemberData(nameof(TestTypes.GetTestCases), MemberType = typeof(TestTypes))]
    public void Roundtrip_Value<T>(TestCase<T> testCase)
    {
        IMessagePackFormatter<T?> formatter = MsgPackSerializer.CreateFormatter(testCase.GetShape(providerUnderTest));

        Sequence<byte> sequence = new();
        MessagePackWriter writer = new(sequence);
        formatter.Serialize(ref writer, testCase.Value, MessagePackSerializerOptions.Standard);
        writer.Flush();

        if (!testCase.HasConstructors(providerUnderTest))
        {
            Assert.Throws<NotSupportedException>(() =>
            {
                MessagePackReader reader = new(sequence);
                return formatter.Deserialize(ref reader, MessagePackSerializerOptions.Standard);
            });
        }
        else
        {
            MessagePackReader reader = new(sequence);
            T? deserializedValue = formatter.Deserialize(ref reader, MessagePackSerializerOptions.Standard);

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
                    deserializedValue = Roundtrip(deserializedValue, formatter);
                }

                Sequence<byte> sequence2 = new();
                MessagePackWriter writer2 = new(sequence2);
                formatter.Serialize(ref writer2, deserializedValue, MessagePackSerializerOptions.Standard);
                writer2.Flush();

                Assert.Equal(sequence, sequence2);
            }
        }
    }

    [GenerateShape]
    public partial record RecordWithParamAndProperty(string Name)
    {
        public int Age { get; init; }
    }

    [GenerateShape<MyEnum>]
    partial class Witness;

    protected void AssertRoundtrip<T>(T? value)
        where T : IShapeable<T> => AssertRoundtrip<T, T>(value);

    protected void AssertRoundtrip<T, TProvider>(T? value)
        where TProvider : IShapeable<T>
    {
        Assert.Equal(value, Roundtrip<T, TProvider>(value));
    }

    protected T? Roundtrip<T>(T? value)
        where T : IShapeable<T> => Roundtrip<T, T>(value);

    protected T? Roundtrip<T, TProvider>(T? value)
        where TProvider : IShapeable<T>
    {
        Sequence<byte> writer = new();
        MsgPackSerializer.Serialize<T, TProvider>(writer, value, MessagePackSerializerOptions.Standard);
        logger.WriteLine(MessagePackSerializer.ConvertToJson(writer));
        return MsgPackSerializer.Deserialize<T, TProvider>(writer, MessagePackSerializerOptions.Standard);
    }

    protected T? Roundtrip<T>(T? value, IMessagePackFormatter<T?> formatter)
    {
        Sequence<byte> sequence = new();
        MessagePackWriter writer = new(sequence);
        formatter.Serialize(ref writer, value, MessagePackSerializerOptions.Standard);
        writer.Flush();

        logger.WriteLine(MessagePackSerializer.ConvertToJson(sequence));

        MessagePackReader reader = new(sequence);
        return formatter.Deserialize(ref reader, MessagePackSerializerOptions.Standard);
    }
}
