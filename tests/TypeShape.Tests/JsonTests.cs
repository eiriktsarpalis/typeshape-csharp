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
    [MemberData(nameof(TestTypes.GetTestValues), MemberType = typeof(TestTypes))]
    public void Roundtrip_Value<T>(T value)
    {
        var serializer = GetSerializerUnderTest<T>();

        string json = serializer.Serialize(value);
        Assert.Equal(ToJsonBaseline(value), json);

        T? deserializedValue = serializer.Deserialize(json);
        Assert.Equal(json, ToJsonBaseline(deserializedValue));
    }

    [Theory]
    [MemberData(nameof(TestTypes.GetTestValues), MemberType = typeof(TestTypes))]
    public void Roundtrip_Property<T>(T value)
    {
        if (!IsReflectionProvider)
            return;

        var serializer = GetSerializerUnderTest<PocoWithGenericProperty<T>>();
        PocoWithGenericProperty<T> poco = new PocoWithGenericProperty<T> { Value = value };

        string json = serializer.Serialize(poco);
        Assert.Equal(ToJsonBaseline(poco), json);

        PocoWithGenericProperty<T> deserializedValue = serializer.Deserialize(json)!;
        Assert.Equal(json, ToJsonBaseline(deserializedValue));
    }

    [Theory]
    [MemberData(nameof(TestTypes.GetTestValues), MemberType = typeof(TestTypes))]
    public void Roundtrip_CollectionElement<T>(T value)
    {
        if (!IsReflectionProvider)
            return;

        var serializer = GetSerializerUnderTest<List<T>>();
        var list = new List<T> { value, value, value };

        string json = serializer.Serialize(list);
        Assert.Equal(ToJsonBaseline(list), json);

        List<T> deserializedValue = serializer.Deserialize(json)!;
        Assert.Equal(json, ToJsonBaseline(deserializedValue));
    }

    [Theory]
    [MemberData(nameof(TestTypes.GetTestValues), MemberType = typeof(TestTypes))]
    public void Roundtrip_DictionaryEntry<T>(T value)
    {
        if (!IsReflectionProvider)
            return;

        var serializer = GetSerializerUnderTest<Dictionary<string, T>>();
        var dict = new Dictionary<string, T> { ["key1"] = value, ["key2"] = value, ["key3"] = value };

        string json = serializer.Serialize(dict);
        Assert.Equal(ToJsonBaseline(dict), json);

        Dictionary<string, T> deserializedValue = serializer.Deserialize(json)!;
        Assert.Equal(json, ToJsonBaseline(deserializedValue));
    }

    [Theory]
    [MemberData(nameof(TestTypes.GetTestValues), MemberType = typeof(TestTypes))]
    public void Roundtrip_Null<T>(T value)
    {
        _ = value;
        if (default(T) is not null)
            return;

        var serializer = GetSerializerUnderTest<T>();
        value = default!;

        string json = serializer.Serialize(value);
        Assert.Equal("null", json);

        T? deserializedValue = serializer.Deserialize(json);
        Assert.Null(deserializedValue);
    }

    [Fact]
    public void Roundtrip_RecordWithNullableDefaultEnum()
    {
        // Can't use STJ as baseline for this type due to 
        // https://github.com/dotnet/runtime/issues/68647

        var serializer = GetSerializerUnderTest<RecordWithNullableDefaultEnum>();
        var value = new RecordWithNullableDefaultEnum();

        string json = serializer.Serialize(value);
        Assert.Equal("""{"flags":"A, B"}""", json);

        value = serializer.Deserialize("{}")!;
        Assert.Equal(MyEnum.A | MyEnum.B, value.flags);
    }

    public class PocoWithGenericProperty<T>
    { 
        public T? Value { get; set; }
    }

    private static string ToJsonBaseline<T>(T? value) => JsonSerializer.Serialize(value, s_baselineOptions);
    private static JsonSerializerOptions s_baselineOptions = new()
    { 
        Converters = { new JsonStringEnumConverter() },
        IncludeFields = true,
    };


    private TypeShapeJsonSerializer<T> GetSerializerUnderTest<T>()
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
}
