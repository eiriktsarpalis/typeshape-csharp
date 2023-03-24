using System.Text.Json;
using System.Text.Json.Serialization;
using TypeShape.Applications.JsonSerializer;
using TypeShape.ReflectionProvider;
using Xunit;

namespace TypeShape.Tests;

public abstract class JsonTests
{
    protected abstract ITypeShapeProvider Provider { get; }

    public bool IsReflectionProvider => Provider is ReflectionTypeShapeProvider;

    [Theory]
    [MemberData(nameof(GetTestCases))]
    public void Roundtrip_Value<T>(TestCase<T> testCase)
    {
        var serializer = GetSerializerUnderTest<T>();

        string json = serializer.Serialize(testCase.Value);
        Assert.Equal(ToJsonBaseline(testCase.Value), json);

        if (testCase.IsAbstractClass)
        {
            Assert.Throws<NotSupportedException>(() => serializer.Deserialize(json));
        }
        else
        {
            T? deserializedValue = serializer.Deserialize(json);
            Assert.Equal(json, ToJsonBaseline(deserializedValue));
        }
    }

    [Theory]
    [MemberData(nameof(GetTestCases))]
    public void Roundtrip_Property<T>(TestCase<T> testCase)
    {
        if (!IsReflectionProvider)
            return;

        var serializer = GetSerializerUnderTest<PocoWithGenericProperty<T>>();
        PocoWithGenericProperty<T> poco = new PocoWithGenericProperty<T> { Value = testCase.Value };

        string json = serializer.Serialize(poco);
        Assert.Equal(ToJsonBaseline(poco), json);

        if (testCase.IsAbstractClass)
        {
            Assert.Throws<NotSupportedException>(() => serializer.Deserialize(json));
        }
        else
        {
            PocoWithGenericProperty<T>? deserializedValue = serializer.Deserialize(json);
            Assert.Equal(json, ToJsonBaseline(deserializedValue));
        }
    }

    [Theory]
    [MemberData(nameof(GetTestCases))]
    public void Roundtrip_CollectionElement<T>(TestCase<T> testCase)
    {
        if (!IsReflectionProvider)
            return;

        var serializer = GetSerializerUnderTest<List<T>>();
        var list = new List<T> { testCase.Value, testCase.Value, testCase.Value };

        string json = serializer.Serialize(list);
        Assert.Equal(ToJsonBaseline(list), json);

        if (testCase.IsAbstractClass)
        {
            Assert.Throws<NotSupportedException>(() => serializer.Deserialize(json));
        }
        else
        {
            List<T>? deserializedValue = serializer.Deserialize(json);
            Assert.Equal(json, ToJsonBaseline(deserializedValue));
        }
    }

    [Theory]
    [MemberData(nameof(GetTestCases))]
    public void Roundtrip_DictionaryEntry<T>(TestCase<T> testCase)
    {
        if (!IsReflectionProvider)
            return;

        var serializer = GetSerializerUnderTest<Dictionary<string, T>>();
        var dict = new Dictionary<string, T> { ["key1"] = testCase.Value, ["key2"] = testCase.Value, ["key3"] = testCase.Value };

        string json = serializer.Serialize(dict);
        Assert.Equal(ToJsonBaseline(dict), json);

        if (testCase.IsAbstractClass)
        {
            Assert.Throws<NotSupportedException>(() => serializer.Deserialize(json));
        }
        else
        {
            Dictionary<string, T>? deserializedValue = serializer.Deserialize(json);
            Assert.Equal(json, ToJsonBaseline(deserializedValue));
        }
    }

    [Theory]
    [MemberData(nameof(GetTestCases))]
    public void Roundtrip_Null<T>(TestCase<T> testCase)
    {
        _ = testCase.Value;
        if (default(T) is not null)
            return;

        var serializer = GetSerializerUnderTest<T>();

        string json = serializer.Serialize(default!);
        Assert.Equal("null", json);

        T? deserializedValue = serializer.Deserialize(json);
        Assert.Null(deserializedValue);
    }

    public class PocoWithGenericProperty<T>
    { 
        public T? Value { get; set; }
    }

    public static IEnumerable<object[]> GetTestCases()
        => TestTypes.GetTestCasesCore()
            .Where(tc => tc is not TestCase<IDiamondInterface>) // Extract to bespoke test until STJ ordering issue is addressed.
            .Select(tc => new object[] { tc })
            .ToArray();

    private static string ToJsonBaseline<T>(T? value) => JsonSerializer.Serialize(value, s_baselineOptions);
    private static readonly JsonSerializerOptions s_baselineOptions = new()
    { 
        Converters = { new JsonStringEnumConverter() },
        IncludeFields = true,
    };


    protected TypeShapeJsonSerializer<T> GetSerializerUnderTest<T>()
    {
        IType<T>? shape = Provider.GetShape<T>();
        Assert.NotNull(shape);
        return TypeShapeJsonSerializer.Create(shape);
    }
}

public sealed class JsonTests_Reflection : JsonTests
{
    protected override ITypeShapeProvider Provider { get; } = new ReflectionTypeShapeProvider(useReflectionEmit: false);
}

public sealed class JsonTests_ReflectionEmit : JsonTests
{
    protected override ITypeShapeProvider Provider { get; } = new ReflectionTypeShapeProvider(useReflectionEmit: true);
}

public sealed class JsonTests_SourceGen : JsonTests
{
    protected override ITypeShapeProvider Provider { get; } = SourceGenTypeShapeProvider.Default;

    [Fact]
    public void LongTuples_SerializedAsFlatJson()
    {
        // Tuples should be serialized as flat JSON, without exposing "Rest" fields.
        // TODO add support in reflection.

        var serializer = GetSerializerUnderTest<(int x1, int x2, int x3, int x4, int x5, int x6, int x7, int x8, int)>();
        var value = (x1: 1, x2: 2, x3: 3, x4: 4, x5: 5, x6: 6, x7: 7, x8: 8, 9);
        string expectedJson = """{"Item1":1,"Item2":2,"Item3":3,"Item4":4,"Item5":5,"Item6":6,"Item7":7,"Item8":8,"Item9":9}""";

        string json = serializer.Serialize(value);
        Assert.Equal(expectedJson, json);

        var deserializedValue = serializer.Deserialize(json);
        Assert.Equal(value, deserializedValue);
    }
}
