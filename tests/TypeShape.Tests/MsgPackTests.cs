using MessagePack;
using Nerdbank.Streams;
using TypeShape.Examples.MsgPackSerializer;
using Xunit;
using Xunit.Abstractions;

namespace TypeShape.Tests;

public partial class MsgPackSerializerTests(ITestOutputHelper logger)
{
    [Fact]
    public void ClassWithPropertySetters() => AssertRoundtrip(new DerivedClass { X = 42, Y = 84 });

    [Fact]
    public void StructWithFields() => AssertRoundtrip(new ComplexStruct { im = 1.2, real = 3.5 });

    [Fact]
    public void SimpleRecord() => AssertRoundtrip(new SimpleRecord(5));

    [Fact]
    public void JustAnEnum() => AssertRoundtrip<MyEnum, Witness>(MyEnum.H);

    [GenerateShape<int>]
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
}
