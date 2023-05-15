using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using static TypeShape.Tests.ValidationTests;

namespace TypeShape.Tests;

public sealed record TestCase<T>(T Value) : ITestCase
{
    Type ITestCase.Type => typeof(T);
    object? ITestCase.Value => Value;
    public bool IsAbstractClass => typeof(T).IsInterface && !typeof(IEnumerable).IsAssignableFrom(typeof(T));
    public bool IsTuple => Value is ITuple;
    public bool IsLongTuple => Value is ITuple t && t.Length > 7;
    public bool IsStack { get; init; }
}

public interface ITestCase
{
    public Type Type { get; }
    public object? Value { get; }
    public bool IsAbstractClass { get; }
}

public static class TestTypes
{

    public static IEnumerable<object[]> GetTestCases()
        => GetTestCasesCore().Select(value => new object[] { value });

    public static IEnumerable<ITestCase> GetTestCasesCore()
    {
        yield return Create(new object());
        yield return Create(false);
        yield return Create("");
        yield return Create("stringValue");
        yield return Create(Rune.GetRuneAt("🤯", 0));
        yield return Create(sbyte.MinValue);
        yield return Create(short.MinValue);
        yield return Create(int.MinValue);
        yield return Create(long.MinValue);
        yield return Create(byte.MaxValue);
        yield return Create(ushort.MaxValue);
        yield return Create(uint.MaxValue);
        yield return Create(ulong.MaxValue);
        yield return Create(Int128.MaxValue);
        yield return Create(UInt128.MaxValue);
        yield return Create(BigInteger.Parse("-170141183460469231731687303715884105728"));
        yield return Create(3.14f);
        yield return Create(3.14d);
        yield return Create(3.14M);
        yield return Create((Half)3.14);
        yield return Create(Guid.Empty);
        yield return Create(DateTime.MaxValue);
        yield return Create(TimeSpan.MaxValue);
        yield return Create(DateOnly.MaxValue);
        yield return Create(TimeOnly.MaxValue);
        yield return Create(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        yield return Create(new int[] { });
        yield return Create(new int[] { 1, 2, 3 });
        yield return Create(new int[][] { new int[] { 1, 0, 0 }, new int[] { 0, 1, 0 }, new int[] { 0, 0, 1 } });
        yield return Create(new List<string> { "1", "2", "3" });
        yield return Create(new List<byte>());
        yield return Create(new Queue<int>(new int[] { 1, 2, 3 }));
        yield return Create(new Stack<int>(new int[] { 1, 2, 3 }), isStack: true);
        yield return Create(new Dictionary<string, int> { ["key1"] = 42, ["key2"] = -1 });
        yield return Create(new HashSet<string> { "apple", "orange", "banana" });
        yield return Create(new HashSet<string> { "apple", "orange", "banana" });
        yield return Create(new SortedSet<string> { "apple", "orange", "banana" });
        yield return Create(new SortedDictionary<string, int> { ["key1"] = 42, ["key2"] = -1 });

        yield return Create(new Hashtable { ["key1"] = 42 });
        yield return Create(new ArrayList { 1, 2, 3 });

        yield return Create(new ConcurrentQueue<int>(new int[] { 1, 2, 3 }));
        yield return Create(new ConcurrentStack<int>(new int[] { 1, 2, 3 }), isStack: true);
        yield return Create(new ConcurrentDictionary<string, string> { ["key"] = "value" });

        yield return Create(ImmutableArray.Create(1, 2, 3));
        yield return Create(ImmutableList.Create("1", "2", "3"));
        yield return Create(ImmutableQueue.Create(1, 2, 3));
        yield return Create(ImmutableStack.Create(1, 2, 3), isStack: true);
        yield return Create(ImmutableHashSet.Create(1, 2, 3));
        yield return Create(ImmutableSortedSet.Create(1, 2, 3));
        yield return Create(ImmutableDictionary.CreateRange(new Dictionary<string, string> { ["key"] = "value" }));
        yield return Create(ImmutableSortedDictionary.CreateRange(new Dictionary<string, string> { ["key"] = "value" }));

        yield return Create(new PocoWithListAndDictionaryProps(@string: "myString")
        {
            List = new() { 1, 2, 3 },
            Dict = new() { ["key1"] = 42, ["key2"] = -1 },
        });

        yield return Create(new BaseClass { X = 1 });
        yield return Create(new DerivedClass { X = 1, Y = 2 });

        var value = new DiamondImplementation { X = 1, Y = 2, Z = 3, W = 4, T = 5 };
        yield return Create<IBaseInterface>(value);
        yield return Create<IDerivedInterface>(value);
        yield return Create<IDerived2Interface>(value);
        yield return Create<IDerived3Interface>(value);
        yield return Create<IDiamondInterface>(value);

        yield return Create(new ParameterlessRecord());
        yield return Create(new ParameterlessStructRecord());

        yield return Create(new SimpleRecord(42));
        yield return Create(new GenericRecord<int>(42));
        yield return Create(new GenericRecord<string>("str"));
        yield return Create(new GenericRecord<GenericRecord<bool>>(new GenericRecord<bool>(true)));

        yield return Create(new ComplexStruct { real = 0, im = 1 });
        yield return Create(new ComplexStructWithProperties { Real = 0, Im = 1 });
        yield return Create(new StructWithDefaultCtor());

        yield return Create(new ValueTuple());
        yield return Create(new ValueTuple<int>(42));
        yield return Create((42, "string"));
        yield return Create((1, 2, 3, 4, 5, 6, 7));
        yield return Create((IntValue: 42, StringValue: "string", BoolValue: true));
        yield return Create((IntValue: 42, StringValue: "string", (1, 0)));
        yield return Create((x1: 1, x2: 2, x3: 3, x4: 4, x5: 5, x6: 6, x7: 7, x8: 8, x9: 9));
        yield return Create((x01: 01, x02: 02, x03: 03, x04: 04, x05: 05, x06: 06, x07: 07, x08: 08, x09: 09, x10: 10, 
                             x11: 11, x12: 12, x13: 13, x14: 14, x15: 15, x16: 16, x17: 17, x18: 18, x19: 19, x20: 20, 
                             x21: 21, x22: 22, x23: 23, x24: 24, x25: 25, x26: 26, x27: 27, x28: 28, x29: 29, x30: 30));

        yield return Create(new Dictionary<int, (int, int)> { [0] = (1,1) });

        yield return Create<Tuple<int>>(new (1));
        yield return Create<Tuple<int, int>>(new (1, 2));
        yield return Create<Tuple<int, string, bool>>(new (1, "str", true));
        yield return Create<Tuple<int, int, int, int, int, int, int>>(new (1, 2, 3, 4, 5, 6, 7));
        yield return Create<Tuple<int, int, int, int, int, int, int, Tuple<int, int, int>>>(new (1, 2, 3, 4, 5, 6, 7, new (8, 9, 10)));
        yield return Create<Tuple<int, int, int, int, int, int, int, Tuple<int, int, int, int, int, int, int, Tuple<int>>>>(new(1,2,3,4,5,6,7,new(8,9,10,11,12,13,14,new(15))));

        yield return Create(new ClassWithReadOnlyField());
        yield return Create(new ClassWithRequiredField { x = 42 });
        yield return Create(new StructWithRequiredField { x = 42 });
        yield return Create(new ClassWithRequiredProperty { X = 42 });
        yield return Create(new StructWithRequiredProperty { X = 42 });
        yield return Create(new StructWithRequiredPropertyAndDefaultCtor { y = 2 });
        yield return Create(new StructWithRequiredFieldAndDefaultCtor { y = 2 });

        yield return Create(new ClassWithSetsRequiredMembersCtor(42));
        yield return Create(new StructWithSetsRequiredMembersCtor(42));

        yield return Create(new ClassWithRequiredAndInitOnlyProperties
        {
            RequiredAndInitOnlyString = "str1",
            RequiredString = "str2",
            InitOnlyString = "str3",

            RequiredAndInitOnlyInt = 1,
            RequiredInt = 2,
            InitOnlyInt = 3,

            requiredField = true,
        });

        yield return Create(new StructWithRequiredAndInitOnlyProperties
        {
            RequiredAndInitOnlyString = "str1",
            RequiredString = "str2",
            InitOnlyString = "str3",

            RequiredAndInitOnlyInt = 1,
            RequiredInt = 2,
            InitOnlyInt = 3,

            requiredField = true,
        });

        yield return Create(new ClassRecordWithRequiredAndInitOnlyProperties(1, 2, 3)
        {
            RequiredAndInitOnlyString = "str1",
            RequiredString = "str2",
            InitOnlyString = "str3",

            RequiredAndInitOnlyInt = 1,
            RequiredInt = 2,
            InitOnlyInt = 3,

            requiredField = true,
        });

        yield return Create(new StructRecordWithRequiredAndInitOnlyProperties(1, 2, 3)
        {
            RequiredAndInitOnlyString = "str1",
            RequiredString = "str2",
            InitOnlyString = "str3",

            RequiredAndInitOnlyInt = 1,
            RequiredInt = 2,
            InitOnlyInt = 3,

            requiredField = true,
        });

        yield return Create(new ClassRecord(0, 1, 2, 3));
        yield return Create(new StructRecord(0, 1, 2, 3));
        yield return Create(new LargeClassRecord());

        yield return Create(new RecordWithDefaultParams());
        yield return Create(new RecordWithDefaultParams2());

        yield return Create(new RecordWithNullableDefaultParams());
        yield return Create(new RecordWithNullableDefaultParams2());

        yield return Create(new RecordWithEnumAndNullableParams(MyEnum.A, MyEnum.C));
        yield return Create(new RecordWithNullableDefaultEnum());

        yield return Create(new GenericContainer<string>.Inner { Value = "str" });
        yield return Create(new GenericContainer<string>.Inner<string> { Value1 = "str", Value2 = "str2" });

        yield return Create(new LinkedList<int>
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
        });

        yield return Create(new RecordWith21ConstructorParameters(
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2",
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2",
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2"));

        yield return Create(new RecordWith42ConstructorParameters(
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2",
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2",
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2",
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2",
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2",
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2"));

        yield return Create(new RecordWith42ConstructorParametersAndRequiredProperties(
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2",
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2",
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2",
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2",
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2",
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2")
        {
            requiredField = 42,
            RequiredProperty = "str"
        });

        yield return Create(new StructRecordWith42ConstructorParametersAndRequiredProperties(
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2",
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2",
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2",
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2",
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2",
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2")
        {
            requiredField = 42,
            RequiredProperty = "str"
        });

        yield return Create(new ClassWith40RequiredMembers
        { 
            r00 = 00, r01 = 01, r02 = 02, r03 = 03, r04 = 04, r05 = 05, r06 = 06, r07 = 07, r08 = 08, r09 = 09, 
            r10 = 10, r11 = 11, r12 = 12, r13 = 13, r14 = 14, r15 = 15, r16 = 16, r17 = 17, r18 = 18, r19 = 19,
            r20 = 20, r21 = 21, r22 = 22, r23 = 23, r24 = 24, r25 = 25, r26 = 26, r27 = 27, r28 = 28, r29 = 29, 
            r30 = 30, r31 = 31, r32 = 32, r33 = 33, r34 = 34, r35 = 35, r36 = 36, r37 = 37, r38 = 38, r39 = 39,
        });

        yield return Create(new StructWith40RequiredMembers
        { 
            r00 = 00, r01 = 01, r02 = 02, r03 = 03, r04 = 04, r05 = 05, r06 = 06, r07 = 07, r08 = 08, r09 = 09, 
            r10 = 10, r11 = 11, r12 = 12, r13 = 13, r14 = 14, r15 = 15, r16 = 16, r17 = 17, r18 = 18, r19 = 19,
            r20 = 20, r21 = 21, r22 = 22, r23 = 23, r24 = 24, r25 = 25, r26 = 26, r27 = 27, r28 = 28, r29 = 29, 
            r30 = 30, r31 = 31, r32 = 32, r33 = 33, r34 = 34, r35 = 35, r36 = 36, r37 = 37, r38 = 38, r39 = 39,
        });

        yield return Create(new StructWith40RequiredMembersAndDefaultCtor
        { 
            r00 = 00, r01 = 01, r02 = 02, r03 = 03, r04 = 04, r05 = 05, r06 = 06, r07 = 07, r08 = 08, r09 = 09, 
            r10 = 10, r11 = 11, r12 = 12, r13 = 13, r14 = 14, r15 = 15, r16 = 16, r17 = 17, r18 = 18, r19 = 19,
            r20 = 20, r21 = 21, r22 = 22, r23 = 23, r24 = 24, r25 = 25, r26 = 26, r27 = 27, r28 = 28, r29 = 29, 
            r30 = 30, r31 = 31, r32 = 32, r33 = 33, r34 = 34, r35 = 35, r36 = 36, r37 = 37, r38 = 38, r39 = 39,
        });

        static TestCase<T> Create<T>(T value, bool isStack = false) => new TestCase<T>(value) { IsStack = isStack };
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

public interface IBaseInterface
{
    public int X { get; set; }
}

public interface IDerivedInterface : IBaseInterface
{
    public int Y { get; set; }
}

public interface IDerived2Interface : IBaseInterface
{ 
    public int Z { get; set; }
}

public interface IDerived3Interface : IBaseInterface
{
    public int W { get; set; }
}

public interface IDiamondInterface : IDerivedInterface, IDerived2Interface, IDerived3Interface
{
    public int T { get; set; }
}

public class DiamondImplementation : IDiamondInterface
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
    public int W { get; set; }
    public int T { get; set; }
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
        init => _value = -1;
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

public class GenericContainer<T>
{
    public class Inner
    {
        public T? Value { get; set; }
    }

    public class Inner<U>
    {
        public T? Value1 { get; set; }
        public U? Value2 { get; set; }
    }
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
[GenerateShape(typeof(Half))]
[GenerateShape(typeof(Int128))]
[GenerateShape(typeof(UInt128))]
[GenerateShape(typeof(Rune))]
[GenerateShape(typeof(Guid))]
[GenerateShape(typeof(DateTime))]
[GenerateShape(typeof(TimeSpan))]
[GenerateShape(typeof(DateOnly))]
[GenerateShape(typeof(TimeOnly))]
[GenerateShape(typeof(BigInteger))]
[GenerateShape(typeof(BindingFlags))]
[GenerateShape(typeof(int[]))]
[GenerateShape(typeof(int[][]))]
[GenerateShape(typeof(List<string>))]
[GenerateShape(typeof(List<byte>))]
[GenerateShape(typeof(Stack<int>))]
[GenerateShape(typeof(Queue<int>))]
[GenerateShape(typeof(Dictionary<string, int>))]
[GenerateShape(typeof(Dictionary<string, string>))]
[GenerateShape(typeof(Dictionary<SimpleRecord, string>))]
[GenerateShape(typeof(Dictionary<string, SimpleRecord>))]
[GenerateShape(typeof(SortedSet<string>))]
[GenerateShape(typeof(SortedDictionary<string, int>))]
[GenerateShape(typeof(ConcurrentStack<int>))]
[GenerateShape(typeof(ConcurrentQueue<int>))]
[GenerateShape(typeof(ConcurrentDictionary<string, string>))]
[GenerateShape(typeof(HashSet<string>))]
[GenerateShape(typeof(Hashtable))]
[GenerateShape(typeof(ArrayList))]
[GenerateShape(typeof(PocoWithListAndDictionaryProps))]
[GenerateShape(typeof(BaseClass))]
[GenerateShape(typeof(DerivedClass))]
[GenerateShape(typeof(IBaseInterface))]
[GenerateShape(typeof(IDerivedInterface))]
[GenerateShape(typeof(IDerived2Interface))]
[GenerateShape(typeof(IDerived3Interface))]
[GenerateShape(typeof(IDiamondInterface))]
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
[GenerateShape(typeof(ImmutableStack<int>))]
[GenerateShape(typeof(ImmutableHashSet<int>))]
[GenerateShape(typeof(ImmutableSortedSet<int>))]
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
[GenerateShape(typeof(GenericContainer<string?>.Inner))]
[GenerateShape(typeof(GenericContainer<string?>.Inner<string?>))]
[GenerateShape(typeof(ValueTuple))]
[GenerateShape(typeof(ValueTuple<int>))]
[GenerateShape(typeof(ValueTuple<int, string>))]
[GenerateShape(typeof(ValueTuple<int, int, int, int, int, int, int, int>))]
[GenerateShape(typeof((int Value, string X)))]
[GenerateShape(typeof((int IntValue, string StringValue, bool BoolValue)))]
[GenerateShape(typeof((int IntValue, string StringValue, (int, int))))]
[GenerateShape(typeof((int x1, int x2, int x3, int x4, int x5, int x6, int x7)))]
[GenerateShape(typeof((int x1, int x2, int x3, int x4, int x5, int x6, int x7, int x8, int x9)))]
[GenerateShape(typeof((int x01, int x02, int x03, int x04, int x05, int x06, int x07, int x08, int x09, int x10, 
                       int x11, int x12, int x13, int x14, int x15, int x16, int x17, int x18, int x19, int x20,
                       int x21, int x22, int x23, int x24, int x25, int x26, int x27, int x28, int x29, int x30)))]
[GenerateShape(typeof(Dictionary<int, (int, int)>))]
[GenerateShape(typeof(Tuple<int>))]
[GenerateShape(typeof(Tuple<int, int>))]
[GenerateShape(typeof(Tuple<int, string, bool>))]
[GenerateShape(typeof(Tuple<int, int, int, int, int, int, int>))]
[GenerateShape(typeof(Tuple<int, int, int, int, int, int, int, int>))]
[GenerateShape(typeof(Tuple<int, int, int, int, int, int, int, Tuple<int, int, int>>))]
[GenerateShape(typeof(Tuple<int, int, int, int, int, int, int, Tuple<int, int, int, int, int, int, int, Tuple<int>>>))]
[GenerateShape(typeof(LinkedList<SimpleRecord?>))]
[GenerateShape(typeof(RecordWith21ConstructorParameters))]
[GenerateShape(typeof(RecordWith42ConstructorParameters))]
[GenerateShape(typeof(RecordWith42ConstructorParametersAndRequiredProperties))]
[GenerateShape(typeof(StructRecordWith42ConstructorParametersAndRequiredProperties))]
[GenerateShape(typeof(ClassWith40RequiredMembers))]
[GenerateShape(typeof(StructWith40RequiredMembers))]
[GenerateShape(typeof(StructWith40RequiredMembersAndDefaultCtor))]
[GenerateShape(typeof(RecordWithNullableDefaultEnum))]
[GenerateShape(typeof(BindingModel))]
[GenerateShape(typeof(List<BindingModel>))]
[GenerateShape(typeof(GenericRecord<BindingModel>))]
[GenerateShape(typeof(Dictionary<string, BindingModel>))]
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