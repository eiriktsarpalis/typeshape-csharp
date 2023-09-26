using BenchmarkDotNet.Attributes;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using TypeShape;
using TypeShape.Applications.JsonSerializer;
using TypeShape.ReflectionProvider;

public static class JsonData
{
    public static readonly MyPoco Value = new MyPoco(@string: "myString")
    {
        List = [1, 2, 3],
        Dict = new() { ["key1"] = 42, ["key2"] = -1 },
    };

    public static readonly string JsonValue = JsonSerializer.Serialize(Value);

    public static readonly JsonTypeInfo<MyPoco> StjReflectionInfo = (JsonTypeInfo<MyPoco>)JsonSerializerOptions.Default.GetTypeInfo(typeof(MyPoco));
    public static readonly JsonTypeInfo<MyPoco> StjSourceGenInfo = StjContext.Default.MyPoco;
    public static readonly JsonTypeInfo<MyPoco> StjSourceGenInfo_fastPath = StjContext_FastPath.Default.MyPoco;

    public static readonly TypeShapeJsonSerializer<MyPoco> TypeShapeReflection = TypeShapeJsonSerializer.Create(ReflectionTypeShapeProvider.Default.GetShape<MyPoco>());
    public static readonly TypeShapeJsonSerializer<MyPoco> TypeShapeSourceGen = TypeShapeJsonSerializer.Create(SourceGenTypeShapeProvider.Default.MyPoco);
}

[MemoryDiagnoser]
public class JsonSerializeBenchmark
{
    [Benchmark(Baseline = true)]
    public string Serialize_StjReflection() => JsonSerializer.Serialize(JsonData.Value, JsonData.StjSourceGenInfo);
    [Benchmark]
    public string Serialize_StjSourceGen() => JsonSerializer.Serialize(JsonData.Value, JsonData.StjSourceGenInfo);
    [Benchmark]
    public string Serialize_StjSourceGen_FastPath() => JsonSerializer.Serialize(JsonData.Value, JsonData.StjSourceGenInfo_fastPath);

    [Benchmark]
    public string Serialize_TypeShapeReflection() => JsonData.TypeShapeReflection.Serialize(JsonData.Value);
    [Benchmark]
    public string Serialize_TypeShapeSourceGen() => JsonData.TypeShapeSourceGen.Serialize(JsonData.Value);
}

[MemoryDiagnoser]
public class JsonDeserializeBenchmark
{
    [Benchmark(Baseline = true)]
    public MyPoco? Deserialize_StjReflection() => JsonSerializer.Deserialize(JsonData.JsonValue, JsonData.StjReflectionInfo);
    [Benchmark]
    public MyPoco? Deserialize_StjSourceGen() => JsonSerializer.Deserialize(JsonData.JsonValue, JsonData.StjSourceGenInfo);

    [Benchmark]
    public MyPoco? Deserialize_TypeShapeReflection() => JsonData.TypeShapeReflection.Deserialize(JsonData.JsonValue);
    [Benchmark]
    public MyPoco? Deserialize_TypeShapeSourceGen() => JsonData.TypeShapeSourceGen.Deserialize(JsonData.JsonValue);
}

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
public partial class SourceGenTypeShapeProvider
{
}

[JsonSerializable(typeof(MyPoco), GenerationMode = JsonSourceGenerationMode.Metadata)]
public partial class StjContext : JsonSerializerContext
{ }

[JsonSerializable(typeof(MyPoco), GenerationMode = JsonSourceGenerationMode.Serialization)]
public partial class StjContext_FastPath : JsonSerializerContext
{ }
