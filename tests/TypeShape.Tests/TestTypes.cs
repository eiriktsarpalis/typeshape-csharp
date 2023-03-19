using System.Collections;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
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

        yield return new ClassWithReadOnlyField();
        yield return new ClassWithRequiredField { x = 42 };
        yield return new StructWithRequiredField { x = 42 };
        yield return new ClassWithRequiredProperty { X = 42 };
        yield return new StructWithRequiredProperty { X = 42 };
        yield return new StructWithRequiredPropertyAndDefaultCtor { y = 2 };
        yield return new StructWithRequiredFieldAndDefaultCtor { y = 2 };

        yield return new ClassWithSetsRequiredMembersCtor(42);
        yield return new StructWithSetsRequiredMembersCtor(42);

        yield return new ClassWithRequiredAndInitOnlyProperties
        {
            RequiredAndInitOnlyString = "str1",
            RequiredString = "str2",
            InitOnlyString = "str3",

            RequiredAndInitOnlyInt = 1,
            RequiredInt = 2,
            InitOnlyInt = 3,

            requiredField = true,
        };

        yield return new StructWithRequiredAndInitOnlyProperties
        {
            RequiredAndInitOnlyString = "str1",
            RequiredString = "str2",
            InitOnlyString = "str3",

            RequiredAndInitOnlyInt = 1,
            RequiredInt = 2,
            InitOnlyInt = 3,

            requiredField = true,
        };

        yield return new ClassRecordWithRequiredAndInitOnlyProperties(1, 2, 3)
        {
            RequiredAndInitOnlyString = "str1",
            RequiredString = "str2",
            InitOnlyString = "str3",

            RequiredAndInitOnlyInt = 1,
            RequiredInt = 2,
            InitOnlyInt = 3,

            requiredField = true,
        };

        yield return new StructRecordWithRequiredAndInitOnlyProperties(1, 2, 3)
        {
            RequiredAndInitOnlyString = "str1",
            RequiredString = "str2",
            InitOnlyString = "str3",

            RequiredAndInitOnlyInt = 1,
            RequiredInt = 2,
            InitOnlyInt = 3,

            requiredField = true,
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

        yield return new RecordWith42ConstructorParametersAndRequiredProperties(
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2",
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2",
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2",
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2",
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2",
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2")
        {
            requiredField = 42,
            RequiredProperty = "str"
        };

        yield return new StructRecordWith42ConstructorParametersAndRequiredProperties(
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2",
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2",
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2",
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2",
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2",
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2")
        {
            requiredField = 42,
            RequiredProperty = "str"
        };

        yield return new ClassWith40RequiredMembers
        { 
            r00 = 00, r01 = 01, r02 = 02, r03 = 03, r04 = 04, r05 = 05, r06 = 06, r07 = 07, r08 = 08, r09 = 09, 
            r10 = 10, r11 = 11, r12 = 12, r13 = 13, r14 = 14, r15 = 15, r16 = 16, r17 = 17, r18 = 18, r19 = 19,
            r20 = 20, r21 = 21, r22 = 22, r23 = 23, r24 = 24, r25 = 25, r26 = 26, r27 = 27, r28 = 28, r29 = 29, 
            r30 = 30, r31 = 31, r32 = 32, r33 = 33, r34 = 34, r35 = 35, r36 = 36, r37 = 37, r38 = 38, r39 = 39,
        };

        yield return new StructWith40RequiredMembers
        { 
            r00 = 00, r01 = 01, r02 = 02, r03 = 03, r04 = 04, r05 = 05, r06 = 06, r07 = 07, r08 = 08, r09 = 09, 
            r10 = 10, r11 = 11, r12 = 12, r13 = 13, r14 = 14, r15 = 15, r16 = 16, r17 = 17, r18 = 18, r19 = 19,
            r20 = 20, r21 = 21, r22 = 22, r23 = 23, r24 = 24, r25 = 25, r26 = 26, r27 = 27, r28 = 28, r29 = 29, 
            r30 = 30, r31 = 31, r32 = 32, r33 = 33, r34 = 34, r35 = 35, r36 = 36, r37 = 37, r38 = 38, r39 = 39,
        };

        yield return new StructWith40RequiredMembersAndDefaultCtor
        { 
            r00 = 00, r01 = 01, r02 = 02, r03 = 03, r04 = 04, r05 = 05, r06 = 06, r07 = 07, r08 = 08, r09 = 09, 
            r10 = 10, r11 = 11, r12 = 12, r13 = 13, r14 = 14, r15 = 15, r16 = 16, r17 = 17, r18 = 18, r19 = 19,
            r20 = 20, r21 = 21, r22 = 22, r23 = 23, r24 = 24, r25 = 25, r26 = 26, r27 = 27, r28 = 28, r29 = 29, 
            r30 = 30, r31 = 31, r32 = 32, r33 = 33, r34 = 34, r35 = 35, r36 = 36, r37 = 37, r38 = 38, r39 = 39,
        };
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

internal class LinkedList<T>
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

public class ClassWithRequiredField
{
    public required int x;
}

public struct StructWithRequiredField
{
    public required int x;
}

public class ClassWithRequiredProperty
{
    public required int X { get; set; }
}

public struct StructWithRequiredProperty
{
    public required int X { get; set; }
}

public class ClassWithReadOnlyField
{
    public readonly int field = 42;
}

public struct StructWithRequiredPropertyAndDefaultCtor
{
    public StructWithRequiredPropertyAndDefaultCtor() { }
    public required int y { get; set; }
}

public struct StructWithRequiredFieldAndDefaultCtor
{
    public StructWithRequiredFieldAndDefaultCtor() { }
    public required int y;
}

public class ClassWithRequiredAndInitOnlyProperties
{
    public required string RequiredAndInitOnlyString { get; init; }
    public required string RequiredString { get; set; }
    public string? InitOnlyString { get; init; }

    public required int RequiredAndInitOnlyInt { get; init; }
    public required int RequiredInt { get; set; }
    public int InitOnlyInt { get; init; }

    public required bool requiredField;

}

public struct StructWithRequiredAndInitOnlyProperties
{
    public required string RequiredAndInitOnlyString { get; init; }
    public required string RequiredString { get; set; }
    public string? InitOnlyString { get; init; }

    public required int RequiredAndInitOnlyInt { get; init; }
    public required int RequiredInt { get; set; }
    public int InitOnlyInt { get; init; }

    public required bool requiredField;
}

public class ClassWithSetsRequiredMembersCtor
{
    private int _value;

    [SetsRequiredMembers]
    public ClassWithSetsRequiredMembersCtor(int value)
    {
        _value = value;
    }

    public required int Value 
    {
        get => _value;
        init => throw new NotSupportedException();
    }
}

public struct StructWithSetsRequiredMembersCtor
{
    private int _value;

    [SetsRequiredMembers]
    public StructWithSetsRequiredMembersCtor(int value)
    {
        _value = value;
    }

    public required int Value
    { 
        get => _value;
        init => throw new NotSupportedException();
    }
}

public record ClassRecordWithRequiredAndInitOnlyProperties(int x, int y, int z)
{
    public required string RequiredAndInitOnlyString { get; init; }
    public required string RequiredString { get; set; }
    public string? InitOnlyString { get; init; }

    public required int RequiredAndInitOnlyInt { get; init; }
    public required int RequiredInt { get; set; }
    public int InitOnlyInt { get; init; }

    public required bool requiredField;
}

public record struct StructRecordWithRequiredAndInitOnlyProperties(int x, int y, int z)
{
    public required string RequiredAndInitOnlyString { get; init; }
    public required string RequiredString { get; set; }
    public string? InitOnlyString { get; init; }

    public required int RequiredAndInitOnlyInt { get; init; }
    public required int RequiredInt { get; set; }
    public int InitOnlyInt { get; init; }

    public required bool requiredField;
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

public record LargeClassRecord(
    int x0 = 0, int x1 = 1, int x2 = 2, int x3 = 3, int x4 = 4, int x5 = 5, int x6 = 5, 
    int x7 = 7, int x8 = 8, string x9 = "str", LargeClassRecord? nested = null);

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

public record RecordWith42ConstructorParametersAndRequiredProperties(
    string x01, int x02, bool x03, TimeSpan x04, DateTime x05, int x06, string x07,
    string x08, int x09, bool x10, TimeSpan x11, DateTime x12, int x13, string x14,
    string x15, int x16, bool x17, TimeSpan x18, DateTime x19, int x20, string x21,
    string x22, int x23, bool x24, TimeSpan x25, DateTime x26, int x27, string x28,
    string x29, int x30, bool x31, TimeSpan x32, DateTime x33, int x34, string x35,
    string x36, int x37, bool x38, TimeSpan x39, DateTime x40, int x41, string x42)
{
    public required int requiredField;
    public required string RequiredProperty { get; set; }
}

public record StructRecordWith42ConstructorParametersAndRequiredProperties(
    string x01, int x02, bool x03, TimeSpan x04, DateTime x05, int x06, string x07,
    string x08, int x09, bool x10, TimeSpan x11, DateTime x12, int x13, string x14,
    string x15, int x16, bool x17, TimeSpan x18, DateTime x19, int x20, string x21,
    string x22, int x23, bool x24, TimeSpan x25, DateTime x26, int x27, string x28,
    string x29, int x30, bool x31, TimeSpan x32, DateTime x33, int x34, string x35,
    string x36, int x37, bool x38, TimeSpan x39, DateTime x40, int x41, string x42)
{
    public required int requiredField;
    public required string RequiredProperty { get; set; }
}

public struct ClassWith40RequiredMembers
{
    public required int r00; public required int r01; public required int r02; public required int r03; public required int r04; public required int r05; public required int r06; public required int r07; public required int r08; public required int r09;
    public required int r10; public required int r11; public required int r12; public required int r13; public required int r14; public required int r15; public required int r16; public required int r17; public required int r18; public required int r19;
    public required int r20; public required int r21; public required int r22; public required int r23; public required int r24; public required int r25; public required int r26; public required int r27; public required int r28; public required int r29;
    public required int r30; public required int r31; public required int r32; public required int r33; public required int r34; public required int r35; public required int r36; public required int r37; public required int r38; public required int r39;
}

public struct StructWith40RequiredMembers
{
    public required int r00; public required int r01; public required int r02; public required int r03; public required int r04; public required int r05; public required int r06; public required int r07; public required int r08; public required int r09;
    public required int r10; public required int r11; public required int r12; public required int r13; public required int r14; public required int r15; public required int r16; public required int r17; public required int r18; public required int r19;
    public required int r20; public required int r21; public required int r22; public required int r23; public required int r24; public required int r25; public required int r26; public required int r27; public required int r28; public required int r29;
    public required int r30; public required int r31; public required int r32; public required int r33; public required int r34; public required int r35; public required int r36; public required int r37; public required int r38; public required int r39;
}

public struct StructWith40RequiredMembersAndDefaultCtor
{
    public StructWith40RequiredMembersAndDefaultCtor() { }
    public required int r00; public required int r01; public required int r02; public required int r03; public required int r04; public required int r05; public required int r06; public required int r07; public required int r08; public required int r09;
    public required int r10; public required int r11; public required int r12; public required int r13; public required int r14; public required int r15; public required int r16; public required int r17; public required int r18; public required int r19;
    public required int r20; public required int r21; public required int r22; public required int r23; public required int r24; public required int r25; public required int r26; public required int r27; public required int r28; public required int r29;
    public required int r30; public required int r31; public required int r32; public required int r33; public required int r34; public required int r35; public required int r36; public required int r37; public required int r38; public required int r39;
}


[GenerateShape(typeof(object))]
[GenerateShape(typeof(bool))]
[GenerateShape(typeof(string))]
[GenerateShape(typeof(sbyte))]
[GenerateShape(typeof(short))]
[GenerateShape(typeof(int))]
[GenerateShape(typeof(long))]
[GenerateShape(typeof(byte))]
[GenerateShape(typeof(ushort))]
[GenerateShape(typeof(uint))]
[GenerateShape(typeof(ulong))]
[GenerateShape(typeof(float))]
[GenerateShape(typeof(double))]
[GenerateShape(typeof(decimal))]
[GenerateShape(typeof(DateTime))]
[GenerateShape(typeof(TimeSpan))]
[GenerateShape(typeof(BindingFlags))]
[GenerateShape(typeof(int[]))]
[GenerateShape(typeof(int[][]))]
[GenerateShape(typeof(List<string>))]
[GenerateShape(typeof(List<byte>))]
[GenerateShape(typeof(Queue<int>))]
[GenerateShape(typeof(Dictionary<string, int>))]
[GenerateShape(typeof(Dictionary<string, string>))]
[GenerateShape(typeof(Dictionary<SimpleRecord, string>))]
[GenerateShape(typeof(Dictionary<string, SimpleRecord>))]
[GenerateShape(typeof(HashSet<string>))]
[GenerateShape(typeof(Hashtable))]
[GenerateShape(typeof(ArrayList))]
[GenerateShape(typeof(PocoWithListAndDictionaryProps))]
[GenerateShape(typeof(BaseClass))]
[GenerateShape(typeof(DerivedClass))]
[GenerateShape(typeof(ParameterlessRecord))]
[GenerateShape(typeof(ParameterlessStructRecord))]
[GenerateShape(typeof(SimpleRecord))]
[GenerateShape(typeof(GenericRecord<int>))]
[GenerateShape(typeof(GenericRecord<string>))]
[GenerateShape(typeof(GenericRecord<GenericRecord<bool>>))]
[GenerateShape(typeof(GenericRecord<GenericRecord<int>>))]
[GenerateShape(typeof(ImmutableArray<int>))]
[GenerateShape(typeof(ImmutableList<string>))]
[GenerateShape(typeof(ImmutableQueue<int>))]
[GenerateShape(typeof(ImmutableDictionary<string, string>))]
[GenerateShape(typeof(ImmutableSortedDictionary<string, string>))]
[GenerateShape(typeof(ComplexStruct))]
[GenerateShape(typeof(ComplexStructWithProperties))]
[GenerateShape(typeof(StructWithDefaultCtor))]
[GenerateShape(typeof(ClassWithReadOnlyField))]
[GenerateShape(typeof(ClassWithRequiredField))]
[GenerateShape(typeof(StructWithRequiredField))]
[GenerateShape(typeof(ClassWithRequiredProperty))]
[GenerateShape(typeof(StructWithRequiredProperty))]
[GenerateShape(typeof(ClassWithRequiredAndInitOnlyProperties))]
[GenerateShape(typeof(StructWithRequiredAndInitOnlyProperties))]
[GenerateShape(typeof(ClassRecordWithRequiredAndInitOnlyProperties))]
[GenerateShape(typeof(StructRecordWithRequiredAndInitOnlyProperties))]
[GenerateShape(typeof(StructWithRequiredPropertyAndDefaultCtor))]
[GenerateShape(typeof(StructWithRequiredFieldAndDefaultCtor))]
[GenerateShape(typeof(ClassWithSetsRequiredMembersCtor))]
[GenerateShape(typeof(StructWithSetsRequiredMembersCtor))]
[GenerateShape(typeof(ClassRecord))]
[GenerateShape(typeof(StructRecord))]
[GenerateShape(typeof(LargeClassRecord))]
[GenerateShape(typeof(RecordWithDefaultParams))]
[GenerateShape(typeof(RecordWithDefaultParams2))]
[GenerateShape(typeof(RecordWithNullableDefaultParams))]
[GenerateShape(typeof(RecordWithNullableDefaultParams2))]
[GenerateShape(typeof(RecordWithEnumAndNullableParams))]
[GenerateShape(typeof(LinkedList<int>))]
//[GenerateShape(typeof((int, string, bool)))] // TODO tuple support
[GenerateShape(typeof(LinkedList<SimpleRecord>))]
[GenerateShape(typeof(RecordWith21ConstructorParameters))]
[GenerateShape(typeof(RecordWith42ConstructorParameters))]
[GenerateShape(typeof(RecordWith42ConstructorParametersAndRequiredProperties))]
[GenerateShape(typeof(StructRecordWith42ConstructorParametersAndRequiredProperties))]
[GenerateShape(typeof(ClassWith40RequiredMembers))]
[GenerateShape(typeof(StructWith40RequiredMembers))]
[GenerateShape(typeof(StructWith40RequiredMembersAndDefaultCtor))]
[GenerateShape(typeof(RecordWithNullableDefaultEnum))]
internal partial class SourceGenTypeShapeProvider
{ }

internal partial class Outer1
{
    public partial class Outer2
    {
        [GenerateShape(typeof(int))]
        [GenerateShape(typeof(Private))]
        private partial class Nested { }

        private class Private { }
    }
}