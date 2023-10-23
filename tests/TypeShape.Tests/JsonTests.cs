using System.Collections;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeShape.Applications.JsonSerializer;
using TypeShape.Applications.JsonSerializer.Converters;
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
        if (testCase.IsLongTuple)
        {
            return; // The STJ baseline doesn't support long tuples -- run in dedicated test below.
        }

        if (testCase.IsMultiDimensionalArray)
        {
            return; // The STJ baseline doesn't support MD arrays -- run in dedicated test below.
        }

        var serializer = GetSerializerUnderTest<T>();

        string json = serializer.Serialize(testCase.Value);
        Assert.Equal(ToJsonBaseline(testCase.Value), json);

        if (!testCase.HasConstructors)
        {
            Assert.Throws<NotSupportedException>(() => serializer.Deserialize(json));
        }
        else
        {
            T? deserializedValue = serializer.Deserialize(json);

            if (testCase.IsStack)
            {
                Assert.Equal(serializer.Serialize(deserializedValue), ToJsonBaseline(deserializedValue));
            }
            else
            {
                Assert.Equal(json, ToJsonBaseline(deserializedValue));
            }
        }
    }

    [Theory]
    [MemberData(nameof(GetTestCases))]
    public void Roundtrip_Property<T>(TestCase<T> testCase)
    {
        if (!IsReflectionProvider)
        {
            return;
        }

        if (testCase.IsLongTuple)
        {
            return; // The STJ baseline doesn't support long tuples -- run in dedicated test below.
        }

        if (testCase.IsMultiDimensionalArray)
        {
            return; // The STJ baseline doesn't support MD arrays -- run in dedicated test below.
        }

        var serializer = GetSerializerUnderTest<PocoWithGenericProperty<T>>();
        PocoWithGenericProperty<T> poco = new PocoWithGenericProperty<T> { Value = testCase.Value };

        string json = serializer.Serialize(poco);
        Assert.Equal(ToJsonBaseline(poco), json);

        if (!testCase.HasConstructors)
        {
            Assert.Throws<NotSupportedException>(() => serializer.Deserialize(json));
        }
        else
        {
            PocoWithGenericProperty<T>? deserializedValue = serializer.Deserialize(json);

            if (testCase.IsStack)
            {
                Assert.Equal(serializer.Serialize(deserializedValue), ToJsonBaseline(deserializedValue));
            }
            else
            {
                Assert.Equal(json, ToJsonBaseline(deserializedValue));
            }
        }
    }

    [Theory]
    [MemberData(nameof(GetTestCases))]
    public void Roundtrip_CollectionElement<T>(TestCase<T> testCase)
    {
        if (!IsReflectionProvider)
        {
            return;
        }

        if (testCase.IsLongTuple)
        {
            return; // The STJ baseline doesn't support long tuples -- run in dedicated test below.
        }

        if (testCase.IsMultiDimensionalArray)
        {
            return; // The STJ baseline doesn't support MD arrays -- run in dedicated test below.
        }

        var serializer = GetSerializerUnderTest<List<T>>();
        var list = new List<T> { testCase.Value, testCase.Value, testCase.Value };

        string json = serializer.Serialize(list);
        Assert.Equal(ToJsonBaseline(list), json);

        if (!testCase.HasConstructors)
        {
            Assert.Throws<NotSupportedException>(() => serializer.Deserialize(json));
        }
        else
        {
            List<T>? deserializedValue = serializer.Deserialize(json);

            if (testCase.IsStack)
            {
                Assert.Equal(serializer.Serialize(deserializedValue), ToJsonBaseline(deserializedValue));
            }
            else
            {
                Assert.Equal(json, ToJsonBaseline(deserializedValue));
            }
        }
    }

    [Theory]
    [MemberData(nameof(GetTestCases))]
    public void Roundtrip_DictionaryEntry<T>(TestCase<T> testCase)
    {
        if (!IsReflectionProvider)
        {
            return;
        }

        if (testCase.IsLongTuple)
        {
            return; // The STJ baseline doesn't support long tuples -- run in dedicated test below.
        }

        if (testCase.IsMultiDimensionalArray)
        {
            return; // The STJ baseline doesn't support MD arrays -- run in dedicated test below.
        }

        var serializer = GetSerializerUnderTest<Dictionary<string, T>>();
        var dict = new Dictionary<string, T> { ["key1"] = testCase.Value, ["key2"] = testCase.Value, ["key3"] = testCase.Value };

        string json = serializer.Serialize(dict);
        Assert.Equal(ToJsonBaseline(dict), json);

        if (!testCase.HasConstructors)
        {
            Assert.Throws<NotSupportedException>(() => serializer.Deserialize(json));
        }
        else
        {
            Dictionary<string, T>? deserializedValue = serializer.Deserialize(json);

            if (testCase.IsStack)
            {
                Assert.Equal(serializer.Serialize(deserializedValue), ToJsonBaseline(deserializedValue));
            }
            else
            {
                Assert.Equal(json, ToJsonBaseline(deserializedValue));
            }
        }
    }

    [Theory]
    [MemberData(nameof(GetTestCases))]
    public void Roundtrip_Null<T>(TestCase<T> testCase)
    {
        if (!testCase.IsNullable)
        {
            return;
        }

        if (testCase.IsMultiDimensionalArray && typeof(T).GetArrayRank() > 2)
        {
            // Rank > 2 support not implemented yet
            return;
        }

        var serializer = GetSerializerUnderTest<T>();

        string json = serializer.Serialize(default!);
        Assert.Equal("null", json);

        T? deserializedValue = serializer.Deserialize(json);
        Assert.Null(deserializedValue);
    }

    [Fact]
    public void Serialize_NonNullablePropertyWithNullValue_ThrowsJsonException()
    {
        var serializer = GetSerializerUnderTest<NonNullStringRecord>();
        var invalidValue = new NonNullStringRecord(null!);
        Assert.Throws<JsonException>(() => serializer.Serialize(invalidValue));
    }

    [Fact]
    public void Deserialize_NonNullablePropertyWithNullJsonValue_ThrowsJsonException()
    {
        var serializer = GetSerializerUnderTest<NonNullStringRecord>();
        Assert.Throws<JsonException>(() => serializer.Deserialize("""{"value":null}"""));
    }

    [Fact]
    public void Serialize_NullablePropertyWithNullValue_WorksAsExpected()
    {
        var serializer = GetSerializerUnderTest<NullableStringRecord>();
        var valueWithNull = new NullableStringRecord(null);
        
        string json = serializer.Serialize(valueWithNull);

        Assert.Equal("""{"value":null}""", json);
    }

    [Fact]
    public void Serialize_NullablePropertyWithNullJsonValue_WorksAsExpected()
    {
        var serializer = GetSerializerUnderTest<NullableStringRecord>();
        
        NullableStringRecord? result = serializer.Deserialize("""{"value":null}""");

        Assert.NotNull(result);
        Assert.Null(result.value);
    }

    [Theory]
    [MemberData(nameof(GetLongTuplesAndExpectedJson))]
    public void LongTuples_SerializedAsFlatJson<Tuple>(Tuple tuple, string expectedJson)
    {
        // Tuples should be serialized as flat JSON, without exposing "Rest" fields.

        var serializer = GetSerializerUnderTest<Tuple>();

        string json = serializer.Serialize(tuple);
        Assert.Equal(expectedJson, json);

        var deserializedValue = serializer.Deserialize(json);
        Assert.Equal(tuple, deserializedValue);
    }

    public static IEnumerable<object?[]> GetLongTuplesAndExpectedJson()
    {
        yield return Wrap(
            (x1: 1, x2: 2, x3: 3, x4: 4, x5: 5, x6: 6, x7: 7, x8: 8, x9: 9),
            """{"Item1":1,"Item2":2,"Item3":3,"Item4":4,"Item5":5,"Item6":6,"Item7":7,"Item8":8,"Item9":9}""");

        yield return Wrap(
            (x1: 1, x2: 2, x3: 3, x4: 4, x5: 5, x6: 6, x7: 7, x8: 8, x9: 9, x10: 10, x11: 11, x12: 12, x13: 13, x14: 14, x15: 15, x16: 16, x17: 17, x18: 18, x19:19, x20:20, x21:21, x22:22, x23:23, x24:24, x25:25, x26:26, x27:27, x28:28, x29:29, x30:30),
            """{"Item1":1,"Item2":2,"Item3":3,"Item4":4,"Item5":5,"Item6":6,"Item7":7,"Item8":8,"Item9":9,"Item10":10,"Item11":11,"Item12":12,"Item13":13,"Item14":14,"Item15":15,"Item16":16,"Item17":17,"Item18":18,"Item19":19,"Item20":20,"Item21":21,"Item22":22,"Item23":23,"Item24":24,"Item25":25,"Item26":26,"Item27":27,"Item28":28,"Item29":29,"Item30":30}""");

        yield return Wrap<Tuple<int, int, int, int, int, int, int, Tuple<int, int, int>>>(
            new(1, 2, 3, 4, 5, 6, 7, new(8, 9, 10)),
            """{"Item1":1,"Item2":2,"Item3":3,"Item4":4,"Item5":5,"Item6":6,"Item7":7,"Item8":8,"Item9":9,"Item10":10}""");

        yield return Wrap<Tuple<int, int, int, int, int, int, int, Tuple<int, int, int, int, int, int, int, Tuple<int>>>>(
            new(1, 2, 3, 4, 5, 6, 7, new(8, 9, 10, 11, 12, 13, 14, new(15))),
            """{"Item1":1,"Item2":2,"Item3":3,"Item4":4,"Item5":5,"Item6":6,"Item7":7,"Item8":8,"Item9":9,"Item10":10,"Item11":11,"Item12":12,"Item13":13,"Item14":14,"Item15":15}""");

        static object?[] Wrap<Tuple>(Tuple tuple, string expectedJson)
            => [tuple, expectedJson];
    }

    [Theory]
    [MemberData(nameof(GetMultiDimensionalArraysAndExpectedJson))]
    public void MultiDimensionalArrays_SerializedAsJaggedArray<TArray>(TArray array, string expectedJson)
        where TArray : IEnumerable
    {
        var serializer = GetSerializerUnderTest<TArray>();

        string json = serializer.Serialize(array);
        Assert.Equal(expectedJson, json);

        TArray? result = serializer.Deserialize(json);
        Assert.Equal(array, result);
    }

    public static IEnumerable<object?[]> GetMultiDimensionalArraysAndExpectedJson()
    {
        yield return Wrap(new int[,] { }, """[]""");

        yield return Wrap(
            new int[,] { { 1, 0, }, { 0, 1 } },
            """[[1,0],[0,1]]""");

        yield return Wrap(
            new int[,] { { 1, 0, 0 }, { 0, 1, 0 }, { 0, 0, 1 } },
            """[[1,0,0],[0,1,0],[0,0,1]]""");

        yield return Wrap(
            new int[,] { { 1, 2, 3 }, { 4, 5, 6 } },
            """[[1,2,3],[4,5,6]]""");

        static object?[] Wrap<TArray>(TArray tuple, string expectedJson) where TArray : IEnumerable
            => [tuple, expectedJson];
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
        IncludeFields = true,
        Converters = 
        { 
            new JsonStringEnumConverter(), 
            new Int128Converter(), 
            new UInt128Converter(),
            new BigIntegerConverter(),
            new HalfConverter(),
            new RuneConverter(),
        },
    };

    protected TypeShapeJsonSerializer<T> GetSerializerUnderTest<T>()
    {
        ITypeShape<T>? shape = Provider.GetShape<T>();
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
}
