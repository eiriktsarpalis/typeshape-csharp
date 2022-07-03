using System.Collections;
using System.Reflection;

namespace TypeShape.Tests;

public static class TestTypes
{
    public static IEnumerable<object[]> GetTestValues()
        => GetTestValuesCore().Select(value => new object[] { value });
    
    public static IEnumerable<object> GetTestValuesCore()
    {
        yield return new object();
        yield return false;
        yield return "";
        yield return "stringValue";
        yield return sbyte.MinValue;
        yield return short.MinValue;
        yield return int.MinValue;
        yield return long.MinValue;
        yield return byte.MaxValue;
        yield return ushort.MaxValue;
        yield return uint.MaxValue;
        yield return ulong.MaxValue;
        yield return 3.14f;
        yield return 3.14d;
        yield return 3.14M;
        yield return DateTime.MaxValue;
        yield return TimeSpan.MaxValue;
        yield return BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        yield return new int[] { };
        yield return new int[] { 1, 2, 3 };
        yield return new int[][] { new int[] { 1, 0, 0 }, new int[] { 0, 1, 0 }, new int[] { 0, 0, 1 } };
        yield return new List<string> { "1", "2", "3" };
        yield return new List<byte>();
        yield return new Queue<int>(new int[] { 1, 2, 3 });
        yield return new Dictionary<string, int> { ["key1"] = 42, ["key2"] = -1 };
        yield return new HashSet<string> { "apple", "orange", "banana" };
        yield return new Hashtable { ["key1"] = 42 };
        yield return new ArrayList { 1, 2, 3 };

        yield return new PocoWithListAndDictionaryProps(@string: "myString")
        {
            List = new() { 1, 2, 3 },
            Dict = new() { ["key1"] = 42, ["key2"] = -1 },
        };

        yield return new BaseClass { X = 1 };
        yield return new DerivedClass { X = 1, Y = 2 };

        yield return new ParameterlessRecord();
        yield return new ParameterlessStructRecord();

        yield return new SimpleRecord(42);
        yield return new GenericRecord<int>(42);
        yield return new GenericRecord<string>("str");
        yield return new GenericRecord<GenericRecord<bool>>(new GenericRecord<bool>(true));

        yield return new ComplexStruct { real = 0, im = 1 };
        yield return new ComplexStructWithProperties { Real = 0, Im = 1 };
        yield return new StructWithDefaultCtor();

        yield return new ClassWithRequiredAndInitOnlyProperties
        {
            RequiredAndInitOnlyString = "str1",
            RequiredString = "str2",
            InitOnlyString = "str3",

            RequiredAndInitOnlyInt = 1,
            RequiredInt = 2,
            InitOnlyInt = 3,
        };

        yield return new StructWithRequiredAndInitOnlyProperties
        {
            RequiredAndInitOnlyString = "str1",
            RequiredString = "str2",
            InitOnlyString = "str3",

            RequiredAndInitOnlyInt = 1,
            RequiredInt = 2,
            InitOnlyInt = 3,
        };

        yield return new ClassRecord(0, 1, 2, 3);
        yield return new StructRecord(0, 1, 2, 3);
        yield return new LargeClassRecord();

        yield return new RecordWithDefaultParams();
        yield return new RecordWithDefaultParams2();

        yield return new RecordWithNullableDefaultParams();
        yield return new RecordWithNullableDefaultParams2();

        yield return new RecordWithEnumAndNullableParams(MyEnum.A, MyEnum.C);

        yield return new LinkedList<int>
        {
            Value = 1,
            Next = new()
            {
                Value = 2,
                Next = new()
                {
                    Value = 3,
                    Next = null,
                }
            }
        };

        yield return new RecordWith21ConstructorParameters(
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2",
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2",
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2");

        yield return new RecordWith42ConstructorParameters(
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2",
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2",
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2",
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2",
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2",
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2");
    }
}

public class PocoWithListAndDictionaryProps
{
    public PocoWithListAndDictionaryProps(bool @bool = true, string @string = "str")
    {
        Bool = @bool;
        String = @string;
    }

    public bool Bool { get; }
    public string String { get; }
    public List<int>? List { get; set; }
    public Dictionary<string, int>? Dict { get; set; }
}

public class LinkedList<T>
{
    public T? Value { get; set; }
    public LinkedList<T>? Next { get; set; }
}

public struct ComplexStruct
{
    public double real;
    public double im;
}

public struct ComplexStructWithProperties
{
    public double Real { get; set; }
    public double Im { get; set; }
}

public struct StructWithDefaultCtor
{
    public int Value;
    public StructWithDefaultCtor()
    {
        Value = 42;
    }
}

public class BaseClass
{
    public int X { get; set; }
}

public class DerivedClass : BaseClass
{
    public int Y { get; set; }
}

public class ClassWithRequiredAndInitOnlyProperties
{
    public required string RequiredAndInitOnlyString { get; init; }
    public required string RequiredString { get; set; }
    public string? InitOnlyString { get; init; }

    public required int RequiredAndInitOnlyInt { get; init; }
    public required int RequiredInt { get; set; }
    public int InitOnlyInt { get; init; }
}

public struct StructWithRequiredAndInitOnlyProperties
{
    public required string RequiredAndInitOnlyString { get; init; }
    public required string RequiredString { get; set; }
    public string? InitOnlyString { get; init; }

    public required int RequiredAndInitOnlyInt { get; init; }
    public required int RequiredInt { get; set; }
    public int InitOnlyInt { get; init; }
}

public record ParameterlessRecord();
public record struct ParameterlessStructRecord();
public record SimpleRecord(int value);
public record GenericRecord<T>(T value);

public record ClassRecord(int x, int? y, int z, int w);
public record struct StructRecord(int x, int y, int z, int w);

public record RecordWithDefaultParams(bool x1 = true, byte x2 = 10, sbyte x3 = 10, char x4 = 'x', ushort x5 = 10, short x6 = 10, long x7 = 10);
public record RecordWithDefaultParams2(ulong x1 = 10, float x2 = 3.1f, double x3 = 3.1d, decimal x4 = -3.1415926m, string x5 = "str", string? x6 = null, object? x7 = null);

public record RecordWithNullableDefaultParams(bool? x1 = true, byte? x2 = 10, sbyte? x3 = 10, char? x4 = 'x', ushort? x5 = 10, short? x6 = 10, long? x7 = 10);
public record RecordWithNullableDefaultParams2(ulong? x1 = 10, float? x2 = 3.1f, double? x3 = 3.1d, decimal? x4 = -3.1415926m, string? x5 = "str", string? x6 = null, object? x7 = null);

[Flags]
public enum MyEnum { A = 1, B = 2, C = 4, D = 8, E = 16, F = 32, G = 64, H = 128 }
public record RecordWithEnumAndNullableParams(MyEnum flags1, MyEnum? flags2, MyEnum flags3 = MyEnum.A, MyEnum? flags4 = null);

public record RecordWithNullableDefaultEnum(MyEnum? flags = MyEnum.A | MyEnum.B);

public record LargeClassRecord(int x0 = 0, int x1 = 1, int x2 = 2, int x3 = 3, int x4 = 4, int x5 = 5, int x6 = 5, int x7 = 7, int x8 = 8, string x9 = "str", LargeClassRecord? nested = null);

public record RecordWith21ConstructorParameters(
    string x01, int x02, bool x03, TimeSpan x04, DateTime x05, int x06, string x07,
    string x08, int x09, bool x10, TimeSpan x11, DateTime x12, int x13, string x14,
    string x15, int x16, bool x17, TimeSpan x18, DateTime x19, int x20, string x21);

public record RecordWith42ConstructorParameters(
    string x01, int x02, bool x03, TimeSpan x04, DateTime x05, int x06, string x07,
    string x08, int x09, bool x10, TimeSpan x11, DateTime x12, int x13, string x14,
    string x15, int x16, bool x17, TimeSpan x18, DateTime x19, int x20, string x21,
    string x22, int x23, bool x24, TimeSpan x25, DateTime x26, int x27, string x28,
    string x29, int x30, bool x31, TimeSpan x32, DateTime x33, int x34, string x35,
    string x36, int x37, bool x38, TimeSpan x39, DateTime x40, int x41, string x42);