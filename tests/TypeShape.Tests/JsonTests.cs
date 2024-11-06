using System.Collections;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeShape.Examples.JsonSerializer;
using TypeShape.Examples.JsonSerializer.Converters;
using Xunit;

namespace TypeShape.Tests;

public abstract partial class JsonTests(IProviderUnderTest providerUnderTest)
{
    [Theory]
    [MemberData(nameof(TestTypes.GetTestCases), MemberType = typeof(TestTypes))]
    public void Roundtrip_Value<T>(TestCase<T> testCase)
    {
        if (IsUnsupportedBySTJ(testCase))
        {
            return;
        }

        JsonConverter<T> converter = GetConverterUnderTest(testCase);

        string json = converter.Serialize(testCase.Value);
        Assert.Equal(ToJsonBaseline(testCase.Value), json);

        if (!providerUnderTest.HasConstructor(testCase) && testCase.Value is not null)
        {
            Assert.Throws<NotSupportedException>(() => converter.Deserialize(json));
        }
        else
        {
            T? deserializedValue = converter.Deserialize(json);
            
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
                
                Assert.Equal(json, ToJsonBaseline(deserializedValue));
            }
        }
    }

    [Theory]
    [MemberData(nameof(TestTypes.GetTestCases), MemberType = typeof(TestTypes))]
    public void Roundtrip_Property<T>(TestCase<T> testCase)
    {
        if (providerUnderTest.Kind is ProviderKind.SourceGen)
        {
            return;
        }

        if (IsUnsupportedBySTJ(testCase))
        {
            return;
        }

        JsonConverter<PocoWithGenericProperty<T>> converter = JsonSerializerTS.CreateConverter(providerUnderTest.UncheckedResolveShape<PocoWithGenericProperty<T>>());
        PocoWithGenericProperty<T> poco = new PocoWithGenericProperty<T> { Value = testCase.Value };

        string json = converter.Serialize(poco);
        Assert.Equal(ToJsonBaseline(poco), json);

        if (!providerUnderTest.HasConstructor(testCase))
        {
            Assert.Throws<NotSupportedException>(() => converter.Deserialize(json));
        }
        else
        {
            PocoWithGenericProperty<T>? deserializedValue = converter.Deserialize(json);
            Assert.NotNull(deserializedValue);

            if (testCase.IsEquatable)
            {
                Assert.Equal(testCase.Value, deserializedValue.Value);
            }
            else
            {
                if (testCase.IsStack)
                {
                    deserializedValue = converter.Deserialize(converter.Serialize(deserializedValue));
                }
                
                Assert.Equal(json, ToJsonBaseline(deserializedValue));
            }
        }
    }

    [Theory]
    [MemberData(nameof(TestTypes.GetTestCases), MemberType = typeof(TestTypes))]
    public void Roundtrip_CollectionElement<T>(TestCase<T> testCase)
    {
        if (providerUnderTest.Kind is ProviderKind.SourceGen)
        {
            return;
        }

        if (IsUnsupportedBySTJ(testCase))
        {
            return;
        }

        var converter = JsonSerializerTS.CreateConverter(providerUnderTest.UncheckedResolveShape<List<T?>>());
        var list = new List<T?> { testCase.Value, testCase.Value, testCase.Value };

        string json = converter.Serialize(list);
        Assert.Equal(ToJsonBaseline(list), json);

        if (!providerUnderTest.HasConstructor(testCase))
        {
            Assert.Throws<NotSupportedException>(() => converter.Deserialize(json));
        }
        else
        {
            List<T?>? deserializedValue = converter.Deserialize(json)!;
            Assert.NotEmpty(deserializedValue);

            if (testCase.IsEquatable)
            {
                Assert.Equal<T?>(list, deserializedValue);
            }
            else
            {
                if (testCase.IsStack)
                {
                    deserializedValue = converter.Deserialize(converter.Serialize(deserializedValue));
                }
                
                Assert.Equal(json, ToJsonBaseline(deserializedValue));
            }
        }
    }

    [Theory]
    [MemberData(nameof(TestTypes.GetTestCases), MemberType = typeof(TestTypes))]
    public void Roundtrip_DictionaryEntry<T>(TestCase<T> testCase)
    {
        if (providerUnderTest.Kind is ProviderKind.SourceGen)
        {
            return;
        }

        if (IsUnsupportedBySTJ(testCase))
        {
            return;
        }

        var converter = JsonSerializerTS.CreateConverter(providerUnderTest.UncheckedResolveShape<Dictionary<string, T?>>());
        var dict = new Dictionary<string, T?> { ["key1"] = testCase.Value, ["key2"] = testCase.Value, ["key3"] = testCase.Value };

        string json = converter.Serialize(dict);
        Assert.Equal(ToJsonBaseline(dict), json);

        if (!providerUnderTest.HasConstructor(testCase))
        {
            Assert.Throws<NotSupportedException>(() => converter.Deserialize(json));
        }
        else
        {
            Dictionary<string, T?>? deserializedValue = converter.Deserialize(json)!;
            Assert.NotEmpty(deserializedValue);

            if (testCase.IsEquatable)
            {
                Assert.Equal<KeyValuePair<string, T?>>(dict, deserializedValue);
            }
            else
            {
                if (testCase.IsStack)
                {
                    deserializedValue = converter.Deserialize(converter.Serialize(deserializedValue));
                }
                
                Assert.Equal(json, ToJsonBaseline(deserializedValue));
            }
        }
    }

    [Fact]
    public void Serialize_NonNullablePropertyWithNullValue_ThrowsJsonException()
    {
        var invalidValue = new NonNullStringRecord(null!);
        var converter = JsonSerializerTS.CreateConverter(providerUnderTest.ResolveShape<NonNullStringRecord>());
        Assert.Throws<JsonException>(() => converter.Serialize(invalidValue));
    }

    [Fact]
    public void Deserialize_NonNullablePropertyWithNullJsonValue_ThrowsJsonException()
    {
        var converter = JsonSerializerTS.CreateConverter(providerUnderTest.ResolveShape<NonNullStringRecord>());
        Assert.Throws<JsonException>(() => converter.Deserialize("""{"value":null}"""));
    }

    [Fact]
    public void Serialize_NullablePropertyWithNullValue_WorksAsExpected()
    {
        var converter = JsonSerializerTS.CreateConverter(providerUnderTest.ResolveShape<NullableStringRecord>());
        var valueWithNull = new NullableStringRecord(null);
        
        string json = converter.Serialize(valueWithNull);

        Assert.Equal("""{"value":null}""", json);
    }

    [Fact]
    public void Serialize_NullablePropertyWithNullJsonValue_WorksAsExpected()
    {
        var converter = JsonSerializerTS.CreateConverter(providerUnderTest.ResolveShape<NullableStringRecord>());
        
        NullableStringRecord? result = converter.Deserialize("""{"value":null}""");

        Assert.NotNull(result);
        Assert.Null(result.value);
    }

    [Theory]
    [MemberData(nameof(GetLongTuplesAndExpectedJson))]
    public void LongTuples_SerializedAsFlatJson<TTuple>(TestCase<TTuple> testCase, string expectedEncoding)
    {
        // Tuples should be serialized as flat JSON, without exposing "Rest" fields.
        var converter = GetConverterUnderTest(testCase);

        string json = converter.Serialize(testCase.Value);
        Assert.Equal(expectedEncoding, json);

        var deserializedValue = converter.Deserialize(json);
        Assert.Equal(testCase.Value, deserializedValue);
    }

    public static IEnumerable<object?[]> GetLongTuplesAndExpectedJson()
    {
        SourceGenProvider p = SourceGenProvider.Default;
        yield return [TestCase.Create(p,
            (x1: 1, x2: 2, x3: 3, x4: 4, x5: 5, x6: 6, x7: 7, x8: 8, x9: 9)),
            """{"Item1":1,"Item2":2,"Item3":3,"Item4":4,"Item5":5,"Item6":6,"Item7":7,"Item8":8,"Item9":9}"""];

        yield return [TestCase.Create(p,
            (x1: 1, x2: 2, x3: 3, x4: 4, x5: 5, x6: 6, x7: 7, x8: 8, x9: 9, x10: 10, x11: 11, x12: 12, x13: 13, x14: 14, x15: 15, x16: 16, x17: 17, x18: 18, x19:19, x20:20, x21:21, x22:22, x23:23, x24:24, x25:25, x26:26, x27:27, x28:28, x29:29, x30:30)),
            """{"Item1":1,"Item2":2,"Item3":3,"Item4":4,"Item5":5,"Item6":6,"Item7":7,"Item8":8,"Item9":9,"Item10":10,"Item11":11,"Item12":12,"Item13":13,"Item14":14,"Item15":15,"Item16":16,"Item17":17,"Item18":18,"Item19":19,"Item20":20,"Item21":21,"Item22":22,"Item23":23,"Item24":24,"Item25":25,"Item26":26,"Item27":27,"Item28":28,"Item29":29,"Item30":30}"""];

        yield return [TestCase.Create(p,
            new Tuple<int, int, int, int, int, int, int, Tuple<int, int, int>>(1, 2, 3, 4, 5, 6, 7, new(8, 9, 10))),
            """{"Item1":1,"Item2":2,"Item3":3,"Item4":4,"Item5":5,"Item6":6,"Item7":7,"Item8":8,"Item9":9,"Item10":10}"""];

        yield return [TestCase.Create(p,
            new Tuple<int, int, int, int, int, int, int, Tuple<int, int, int, int, int, int, int, Tuple<int>>>(1, 2, 3, 4, 5, 6, 7, new(8, 9, 10, 11, 12, 13, 14, new(15)))),
            """{"Item1":1,"Item2":2,"Item3":3,"Item4":4,"Item5":5,"Item6":6,"Item7":7,"Item8":8,"Item9":9,"Item10":10,"Item11":11,"Item12":12,"Item13":13,"Item14":14,"Item15":15}"""];
    }

    [Theory]
    [MemberData(nameof(GetMultiDimensionalArraysAndExpectedJson))]
    public void MultiDimensionalArrays_SerializedAsJaggedArray<TArray>(TestCase<TArray> testCase, string expectedEncoding)
        where TArray : IEnumerable
    {
        var converter = GetConverterUnderTest(testCase);

        string json = converter.Serialize(testCase.Value);
        Assert.Equal(expectedEncoding, json);

        TArray? result = converter.Deserialize(json);
        Assert.Equal(testCase.Value, result);
    }

    [GenerateShape<int[,]>]
    [GenerateShape<int[,,]>]
    [GenerateShape<int[,,,,,]>]
    [GenerateShape<(int, int, int, int, int, int, int, int, int)>]
    [GenerateShape<(int, int, int, int, int, int, int, int, int, int,
    int, int, int, int, int, int, int, int, int, int,
    int, int, int, int, int, int, int, int, int, int)>]
    [GenerateShape<Tuple<int, int, int, int, int, int, int, Tuple<int, int, int>>>]
    [GenerateShape<Tuple<int, int, int, int, int, int, int, Tuple<int, int, int, int, int, int, int, Tuple<int>>>>]
    internal partial class SourceGenProvider;

    public static IEnumerable<object?[]> GetMultiDimensionalArraysAndExpectedJson()
    {
        SourceGenProvider p = SourceGenProvider.Default;
        yield return [TestCase.Create(p, new int[,] { }), """[]"""];
        yield return [TestCase.Create(p, new int[,,] { }), """[]"""];
        yield return [TestCase.Create(p, new int[,,,,,] { }), """[]"""];

        yield return [TestCase.Create(p,
            new int[,] { { 1, 0, }, { 0, 1 } }),
            """[[1,0],[0,1]]"""];

        yield return [TestCase.Create(p,
            new int[,] { { 1, 0, 0 }, { 0, 1, 0 }, { 0, 0, 1 } }),
            """[[1,0,0],[0,1,0],[0,0,1]]"""];

        yield return [TestCase.Create(p,
            new int[,] { { 1, 2, 3 }, { 4, 5, 6 } }),
            """[[1,2,3],[4,5,6]]"""];
        
        yield return [TestCase.Create(p, 
            new int[,,] // 3 x 2 x 2
            {
                { { 1, 0 }, { 0, 1 } }, 
                { { 1, 2 }, { 3, 4 } }, 
                { { 1, 1 }, { 1, 1 } }
            }),
            """[[[1,0],[0,1]],[[1,2],[3,4]],[[1,1],[1,1]]]"""];
        
        yield return [TestCase.Create(p,
            new int[,,] // 3 x 2 x 5
            {
                { { 1, 0, 0, 0, 0 }, { 0, 1, 0, 0, 0 } }, 
                { { 1, 2, 3, 4, 5 }, { 6, 7, 8, 9, 10 } }, 
                { { 1, 1, 1, 1, 1 }, { 1, 1, 1, 1, 1 } }
            }),
            """[[[1,0,0,0,0],[0,1,0,0,0]],[[1,2,3,4,5],[6,7,8,9,10]],[[1,1,1,1,1],[1,1,1,1,1]]]"""];
    }

    [Fact]
    public void Roundtrip_DerivedClassWithVirtualProperties()
    {
        const string ExpectedJson = """{"X":42,"Y":"str","Z":42,"W":0}""";
        var converter = JsonSerializerTS.CreateConverter(providerUnderTest.ResolveShape<DerivedClassWithVirtualProperties>());

        var value = new DerivedClassWithVirtualProperties();
        string json = converter.Serialize(value);
        Assert.Equal(ExpectedJson, json);
    }

    [Fact]
    public void ClassWithInitOnlyProperties_MissingPayloadPreservesDefaultValues()
    {
        var converter = JsonSerializerTS.CreateConverter(providerUnderTest.ResolveShape<ClassWithInitOnlyProperties>());
        int expectedValue = new ClassWithInitOnlyProperties().Value;
        List<int> expectedValues = new ClassWithInitOnlyProperties().Values;

        ClassWithInitOnlyProperties? result = converter.Deserialize("{}");
        Assert.Equal(expectedValue, result?.Value);
        Assert.Equal(expectedValues, result?.Values);
    }

    public class PocoWithGenericProperty<T>
    { 
        public T? Value { get; set; }
    }

    protected static string ToJsonBaseline<T>(T? value) => System.Text.Json.JsonSerializer.Serialize(value, s_baselineOptions);
    private static readonly JsonSerializerOptions s_baselineOptions = new()
    { 
        IncludeFields = true,
        Converters = 
        { 
            new JsonStringEnumConverter(),
            new BigIntegerConverter(),
            new RuneConverter(),
        },
    };

    private JsonConverter<T> GetConverterUnderTest<T>(TestCase<T> testCase) =>
        JsonSerializerTS.CreateConverter<T>(providerUnderTest.ResolveShape(testCase));

    private protected static bool IsUnsupportedBySTJ<T>(TestCase<T> value) => 
        value.IsMultiDimensionalArray ||
        value.IsLongTuple ||
        value.HasRefConstructorParameters ||
        value.Value is DerivedClassWithVirtualProperties; // https://github.com/dotnet/runtime/issues/96996
}

public sealed class JsonTests_Reflection() : JsonTests(RefectionProviderUnderTest.Default);
public sealed class JsonTests_ReflectionEmit() : JsonTests(RefectionProviderUnderTest.NoEmit);
public sealed class JsonTests_SourceGen() : JsonTests(SourceGenProviderUnderTest.Default);