using BenchmarkDotNet.Attributes;
using MessagePack;
using Nerdbank.Streams;
using System.Buffers;
using TypeShape.Examples.MsgPackSerializer;

namespace TypeShape.Benchmarks;

public static partial class MsgPackData
{
    public static readonly Person Alice = new Person { Name = "Alice", Age = 30 };

    public static readonly ReadOnlySequence<byte> AliceMsgPack = new(MessagePackSerializer.Serialize(Alice, MessagePackSerializerOptions.Standard));

    [GenerateShape, MessagePackObject(true)]
    public partial struct Person : IEquatable<Person>
    {
        public string? Name { get; set; }

        public int Age { get; set; }

        public bool Equals(Person other) => Name == other.Name && Age == other.Age;
    }
}

[MemoryDiagnoser]
public class MsgPackSerializationBenchmark
{
    private readonly Sequence<byte> sequence = new();

    [Benchmark]
    public void Serialize_TypeShape()
    {
        MsgPackSerializer.Serialize(sequence, MsgPackData.Alice);
        sequence.Reset();
    }

    [Benchmark(Baseline = true)]
    public void Serialize_Library()
    {
        MessagePackSerializer.Serialize(sequence, MsgPackData.Alice, MessagePackSerializerOptions.Standard);
        sequence.Reset();
    }
}

[MemoryDiagnoser]
public class MsgPackDeserializationBenchmark
{
    private readonly Sequence<byte> sequence = new();

    [Benchmark]
    public void Deserialize_TypeShape()
    {
        MsgPackSerializer.Deserialize<MsgPackData.Person>(MsgPackData.AliceMsgPack);
    }

    [Benchmark(Baseline = true)]
    public void Deserialize_Library()
    {
        MessagePackSerializer.Deserialize<MsgPackData.Person>(MsgPackData.AliceMsgPack, MessagePackSerializerOptions.Standard);
    }
}
