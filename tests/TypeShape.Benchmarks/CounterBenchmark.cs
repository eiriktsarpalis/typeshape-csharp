using BenchmarkDotNet.Attributes;
using TypeShape.Applications.Counter;
using TypeShape.ReflectionProvider;

namespace TypeShape.Benchmarks;

[MemoryDiagnoser]
public partial class CounterBenchmark
{
    private readonly MyPoco _value = new MyPoco(@string: "myString")
    {
        List = new() { 1, 2, 3 },
        Dict = new() { ["key1"] = 42, ["key2"] = -1 },
    };

    private readonly Func<MyPoco, long> _sourceGenCounter = Counter.Create(SourceGenTypeShapeProvider.Default.CounterBenchmark_MyPoco);
    private readonly Func<MyPoco, long> _reflectionEmitCounter = Counter.Create(new ReflectionTypeShapeProvider(useReflectionEmit: true).GetShape<MyPoco>());
    private readonly Func<MyPoco, long> _reflectionCounter = Counter.Create(new ReflectionTypeShapeProvider(useReflectionEmit: false).GetShape<MyPoco>());

    [Benchmark(Baseline = true)]
    public long Baseline()
    {
        return Count(_value);

        static long Count(MyPoco? value)
        {
            if (value is null)
            {
                return 0;
            }

            long count = 1;

            count++; // value.Bool != null

            if (value.String != null)
            {
                count++;
            }

            if (value.List != null)
            {
                count++;

                foreach (int _ in value.List)
                {
                    count++;
                }
            }

            if (value.Dict != null)
            {
                count++;

                foreach (KeyValuePair<string, int> entry in value.Dict)
                {
                    if (entry.Key != null)
                    {
                        count++;
                    }

                    count++; // entry.Value != null
                }
            }

            return count;
        }
    }

    [Benchmark]
    public long ReflectionEmit() => _reflectionEmitCounter(_value);
    [Benchmark]
    public long Reflection() => _reflectionCounter(_value);
    [Benchmark]
    public long SourceGen() => _sourceGenCounter(_value);

    public class MyPoco
    {
        public MyPoco(bool @bool = true, string @string = "str")
        {
            Bool = @bool;
            String = @string;
        }

        public bool Bool { get; }
        public string String { get; }
        public List<int>? List { get; set; }
        public Dictionary<string, int>? Dict { get; set; }
    }

    [GenerateShape(typeof(MyPoco))]
    public partial class SourceGenTypeShapeProvider { }
}