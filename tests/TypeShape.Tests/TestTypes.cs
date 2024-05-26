using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.FSharp.Collections;
using TypeShape.Tests.FSharp;

namespace TypeShape.Tests;

public static class TestTypes
{
    public static IEnumerable<object[]> GetTestCases() => 
        GetTestCasesWithExpendedValues()
        .Select(value => new object[] { value });
    
    public static IEnumerable<object[]> GetEqualValuePairs() => 
        GetTestCasesWithExpendedValues()
        .Zip(GetTestCasesWithExpendedValues(), (l, r) => new object[] { l, r });

    public static IEnumerable<ITestCase> GetTestCasesWithExpendedValues() =>
        GetTestCasesCore().SelectMany(testCase => testCase.ExpandCases());

    public static IEnumerable<ITestCase> GetTestCasesCore()
    {
        SourceGenProvider p = SourceGenProvider.Default;
        yield return TestCase.Create(p, new object());
        yield return TestCase.Create(p, false);
        yield return TestCase.Create(p, "stringValue", additionalValues: [""]);
        yield return TestCase.Create(p, Rune.GetRuneAt("🤯", 0));
        yield return TestCase.Create(p, sbyte.MinValue);
        yield return TestCase.Create(p, short.MinValue);
        yield return TestCase.Create(p, int.MinValue);
        yield return TestCase.Create(p, long.MinValue);
        yield return TestCase.Create(p, byte.MaxValue);
        yield return TestCase.Create(p, ushort.MaxValue);
        yield return TestCase.Create(p, uint.MaxValue);
        yield return TestCase.Create(p, ulong.MaxValue);
        yield return TestCase.Create(p, Int128.MaxValue);
        yield return TestCase.Create(p, UInt128.MaxValue);
        yield return TestCase.Create(p, BigInteger.Parse("-170141183460469231731687303715884105728"));
        yield return TestCase.Create(p, 3.14f);
        yield return TestCase.Create(p, 3.14d);
        yield return TestCase.Create(p, 3.14M);
        yield return TestCase.Create(p, (Half)3.14);
        yield return TestCase.Create(p, Guid.Empty);
        yield return TestCase.Create(p, DateTime.MaxValue);
        yield return TestCase.Create(p, DateTimeOffset.MaxValue);
        yield return TestCase.Create(p, TimeSpan.MaxValue);
        yield return TestCase.Create(p, DateOnly.MaxValue);
        yield return TestCase.Create(p, TimeOnly.MaxValue);
        yield return TestCase.Create(p, new Uri("https://github.com"));
        yield return TestCase.Create(p, new Version("1.0.0.0"));
        yield return TestCase.Create(p, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        
        yield return TestCase.Create(p, (bool?)false);
        yield return TestCase.Create(p, (Rune?)Rune.GetRuneAt("🤯", 0));
        yield return TestCase.Create(p, (sbyte?)sbyte.MinValue);
        yield return TestCase.Create(p, (short?)short.MinValue);
        yield return TestCase.Create(p, (int?)int.MinValue);
        yield return TestCase.Create(p, (long?)long.MinValue);
        yield return TestCase.Create(p, (byte?)byte.MaxValue);
        yield return TestCase.Create(p, (ushort?)ushort.MaxValue);
        yield return TestCase.Create(p, (uint?)uint.MaxValue);
        yield return TestCase.Create(p, (ulong?)ulong.MaxValue);
        yield return TestCase.Create(p, (Int128?)Int128.MaxValue);
        yield return TestCase.Create(p, (UInt128?)UInt128.MaxValue);
        yield return TestCase.Create(p, (BigInteger?)BigInteger.Parse("-170141183460469231731687303715884105728"));
        yield return TestCase.Create(p, (float?)3.14f);
        yield return TestCase.Create(p, (double?)3.14d);
        yield return TestCase.Create(p, (decimal?)3.14M);
        yield return TestCase.Create(p, (Half?)3.14);
        yield return TestCase.Create(p, (Guid?)Guid.Empty);
        yield return TestCase.Create(p, (DateTime?)DateTime.MaxValue);
        yield return TestCase.Create(p, (DateTimeOffset?)DateTimeOffset.MaxValue);
        yield return TestCase.Create(p, (TimeSpan?)TimeSpan.MaxValue);
        yield return TestCase.Create(p, (DateOnly?)DateOnly.MaxValue);
        yield return TestCase.Create(p, (TimeOnly?)TimeOnly.MaxValue);
        yield return TestCase.Create(p, (BindingFlags?)BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        
        yield return TestCase.Create(p, (int[])[1, 2, 3], additionalValues: [new int[0]]);
        yield return TestCase.Create(p, (int[][])[[1, 0, 0], [0, 1, 0], [0, 0, 1]], additionalValues: [[new int[0]]]);
        yield return TestCase.Create(p, (byte[])[1, 2, 3]);
        yield return TestCase.Create(p, (Memory<int>)new int[] { 1, 2, 3 });
        yield return TestCase.Create(p, (ReadOnlyMemory<int>)new[] { 1, 2, 3 });
        yield return TestCase.Create(p, (List<string>)["1", "2", "3"]);
        yield return TestCase.Create(p, (List<byte>)[1, 2, 3], additionalValues: [[]]);
        yield return TestCase.Create(p, new LinkedList<byte>([1, 2, 3]), additionalValues: [[]]);
        yield return TestCase.Create(p, new Queue<int>([1, 2, 3]));
        yield return TestCase.Create(p, new Stack<int>([1, 2, 3]), isStack: true);
        yield return TestCase.Create(p, new Dictionary<string, int> { ["key1"] = 42, ["key2"] = -1 });
        yield return TestCase.Create(p, (HashSet<string>)["apple", "orange", "banana"]);
        yield return TestCase.Create(p, (SortedSet<string>)["apple", "orange", "banana"]);
        yield return TestCase.Create(p, new SortedDictionary<string, int> { ["key1"] = 42, ["key2"] = -1 });

        yield return TestCase.Create(p, new Hashtable { ["key1"] = 42 }, additionalValues: [[]]);
        yield return TestCase.Create(p, new ArrayList { 1, 2, 3 }, additionalValues: [[]]);

        yield return TestCase.Create(p, new int[,] { { 1, 0, 0 }, { 0, 1, 0 }, { 0, 0, 1 } });
        yield return TestCase.Create(p, new int[,,] { { { 1 } } });

        yield return TestCase.Create(p, new ConcurrentQueue<int>([1, 2, 3]));
        yield return TestCase.Create(p, new ConcurrentStack<int>([1, 2, 3]), isStack: true);
        yield return TestCase.Create(p, new ConcurrentDictionary<string, string> { ["key"] = "value" });

        yield return TestCase.Create(p, (IEnumerable)new List<object> { 1, 2, 3 });
        yield return TestCase.Create(p, (IList)new List<object> { 1, 2, 3 });
        yield return TestCase.Create(p, (ICollection)new List<object> { 1, 2, 3 });
        yield return TestCase.Create(p, (IDictionary)new Dictionary<object, object> { [42] = 42 });
        yield return TestCase.Create(p, (IEnumerable<int>)[1, 2, 3]);
        yield return TestCase.Create(p, (ICollection<int>)[1, 2, 3]);
        yield return TestCase.Create(p, (IList<int>)[1, 2, 3]);
        yield return TestCase.Create(p, (IReadOnlyCollection<int>)[1, 2, 3]);
        yield return TestCase.Create(p, (IReadOnlyList<int>)[1, 2, 3]);
        yield return TestCase.Create(p, (ISet<int>)new HashSet<int> { 1, 2, 3 });
        yield return TestCase.Create(p, (IReadOnlySet<int>)new HashSet<int> { 1, 2, 3 });
        yield return TestCase.Create(p, (IDictionary<int, int>)new Dictionary<int, int> { [42] = 42 });
        yield return TestCase.Create(p, (IReadOnlyDictionary<int, int>)new Dictionary<int, int> { [42] = 42 });

        yield return TestCase.Create(new DerivedList { 1, 2, 3 });
        yield return TestCase.Create(new DerivedDictionary { ["key"] = "value" });

        yield return TestCase.Create(p, new StructList<int> { 1, 2, 3 });
        yield return TestCase.Create(p, new StructDictionary<string, string> { ["key"] = "value" });
        yield return TestCase.Create<CollectionWithBuilderAttribute>([1, 2, 3]);
        yield return TestCase.Create(p, (GenericCollectionWithBuilderAttribute<int>)[1, 2, 3]);
        yield return TestCase.Create(new CollectionWithEnumerableCtor([1, 2, 3]));
        yield return TestCase.Create(new DictionaryWithEnumerableCtor([new("key", 42)]));
        yield return TestCase.Create(new CollectionWithSpanCtor([1, 2, 3]), usesSpanCtor: true);
        yield return TestCase.Create(new DictionaryWithSpanCtor([new("key", 42)]), usesSpanCtor: true);

        yield return TestCase.Create(p, new Collection<int> { 1, 2, 3 });
        yield return TestCase.Create(p, new ObservableCollection<int> { 1, 2, 3 });
        yield return TestCase.Create(p, new MyKeyedCollection<int> { 1, 2, 3 });
        yield return TestCase.Create(p, new MyKeyedCollection<string> { "1", "2", "3" });
        yield return TestCase.Create(p, new ReadOnlyCollection<int>([1, 2, 3]));
        yield return TestCase.Create(p, new ReadOnlyDictionary<int, int>(new Dictionary<int, int> { [1] = 1, [2] = 2 }));

        yield return TestCase.Create(p, (ImmutableArray<int>)[1, 2, 3]);
        yield return TestCase.Create(p, (ImmutableList<string>)["1", "2", "3"]);
        yield return TestCase.Create(p, (ImmutableList<string?>)["1", "2", null]);
        yield return TestCase.Create(p, (ImmutableQueue<int>)[1, 2, 3]);
        yield return TestCase.Create(p, (ImmutableStack<int>)[1, 2, 3], isStack: true);
        yield return TestCase.Create(p, (ImmutableHashSet<int>)[1, 2, 3]);
        yield return TestCase.Create(p, (ImmutableSortedSet<int>)[1, 2, 3]);
        yield return TestCase.Create(p, ImmutableDictionary.CreateRange(new Dictionary<string, string> { ["key"] = "value" }));
        yield return TestCase.Create(p, ImmutableDictionary.CreateRange(new Dictionary<string, string?> { ["key"] = null }));
        yield return TestCase.Create(p, ImmutableSortedDictionary.CreateRange(new Dictionary<string, string> { ["key"] = "value" }));

        yield return TestCase.Create(new PocoWithListAndDictionaryProps(@string: "myString")
        {
            List = [1, 2, 3],
            Dict = new() { ["key1"] = 42, ["key2"] = -1 },
        });

        yield return TestCase.Create(new BaseClass { X = 1 });
        yield return TestCase.Create(new DerivedClass { X = 1, Y = 2 });
        yield return TestCase.Create(new DerivedClassWithVirtualProperties());

        var value = new DiamondImplementation { X = 1, Y = 2, Z = 3, W = 4, T = 5 };
        yield return TestCase.Create<IBaseInterface>(value);
        yield return TestCase.Create<IDerivedInterface>(value);
        yield return TestCase.Create<IDerived2Interface>(value);
        yield return TestCase.Create<IDerived3Interface>(value);
        yield return TestCase.Create<IDiamondInterface>(value);

        yield return TestCase.Create(new ParameterlessRecord());
        yield return TestCase.Create(new ParameterlessStructRecord());

        yield return TestCase.Create(new ClassWithNullabilityAttributes());
        yield return TestCase.Create(p, new ClassWithNullabilityAttributes<string> 
        { 
            NotNullField = "str", 
            DisallowNullField = "str", 
            DisallowNullProperty = "str", 
            NotNullProperty = "str" 
        });

        yield return TestCase.Create(p, new ClassWithNotNullProperty<string> { Property = "Value" });
        yield return TestCase.Create(new ClassWithStructNullabilityAttributes());
        yield return TestCase.Create(new ClassWithInternalConstructor(42));
        yield return TestCase.Create(new NonNullStringRecord("str"));
        yield return TestCase.Create(new NullableStringRecord(null));
        yield return TestCase.Create(p, new NotNullGenericRecord<string>("str"));
        yield return TestCase.Create(p, new NotNullClassGenericRecord<string>("str"));
        yield return TestCase.Create(p, new NullClassGenericRecord<string>("str"));
        yield return TestCase.Create(p, new NullObliviousGenericRecord<string>("str"));

        yield return TestCase.Create(new SimpleRecord(42));
        yield return TestCase.Create(p, new GenericRecord<int>(42));
        yield return TestCase.Create(p, new GenericRecord<string>("str"));
        yield return TestCase.Create(p, new GenericRecord<GenericRecord<bool>>(new GenericRecord<bool>(true)));
        yield return TestCase.Create(p, new GenericRecordStruct<int>(42));
        yield return TestCase.Create(p, new GenericRecordStruct<string>("str"));
        yield return TestCase.Create(p, new GenericRecordStruct<GenericRecordStruct<bool>>(new GenericRecordStruct<bool>(true)));
        yield return TestCase.Create(p, new GenericRecordStruct<string>("str"));
        yield return TestCase.Create(p, new GenericRecordStruct<GenericRecordStruct<bool>>(new GenericRecordStruct<bool>(true)));

        yield return TestCase.Create(new ClassWithInitOnlyProperties { Value = 99, Values = [99] });
        yield return TestCase.Create(p, new GenericStructWithInitOnlyProperty<int> { Value = 42 });
        yield return TestCase.Create(p, new GenericStructWithInitOnlyProperty<string> { Value = "str" });
        yield return TestCase.Create(p, new GenericStructWithInitOnlyProperty<GenericStructWithInitOnlyProperty<string>> { Value = new() { Value = "str" } });

        yield return TestCase.Create(new ComplexStruct { real = 0, im = 1 });
        yield return TestCase.Create(new ComplexStructWithProperties { Real = 0, Im = 1 });
        yield return TestCase.Create(new StructWithDefaultCtor());

        yield return TestCase.Create(p, new ValueTuple());
        yield return TestCase.Create(p, new ValueTuple<int>(42));
        yield return TestCase.Create(p, (42, "string"));
        yield return TestCase.Create(p, (1, 2, 3, 4, 5, 6, 7));
        yield return TestCase.Create(p, (IntValue: 42, StringValue: "string", BoolValue: true));
        yield return TestCase.Create(p, (IntValue: 42, StringValue: "string", (1, 0)));
        yield return TestCase.Create(p, (x1: 1, x2: 2, x3: 3, x4: 4, x5: 5, x6: 6, x7: 7, x8: 8, x9: 9));
        yield return TestCase.Create(p, (x01: 01, x02: 02, x03: 03, x04: 04, x05: 05, x06: 06, x07: 07, x08: 08, x09: 09, x10: 10,
                             x11: 11, x12: 12, x13: 13, x14: 14, x15: 15, x16: 16, x17: 17, x18: 18, x19: 19, x20: 20,
                             x21: 21, x22: 22, x23: 23, x24: 24, x25: 25, x26: 26, x27: 27, x28: 28, x29: 29, x30: 30));

        yield return TestCase.Create(p, new Dictionary<int, (int, int)> { [0] = (1, 1) });

        yield return TestCase.Create(p, new Tuple<int>(1));
        yield return TestCase.Create(p, new Tuple<int, int>(1, 2));
        yield return TestCase.Create(p, new Tuple<int, string, bool>(1, "str", true));
        yield return TestCase.Create(p, new Tuple<int, int, int, int, int, int, int>(1, 2, 3, 4, 5, 6, 7));
        yield return TestCase.Create(p, new Tuple<int, int, int, int, int, int, int, Tuple<int, int, int>>(1, 2, 3, 4, 5, 6, 7, new(8, 9, 10)));
        yield return TestCase.Create(p, new Tuple<int, int, int, int, int, int, int, Tuple<int, int, int, int, int, int, int, Tuple<int>>>(1, 2, 3, 4, 5, 6, 7, new(8, 9, 10, 11, 12, 13, 14, new(15))));

        yield return TestCase.Create(new ClassWithReadOnlyField());
        yield return TestCase.Create(new ClassWithRequiredField { x = 42 });
        yield return TestCase.Create(new StructWithRequiredField { x = 42 });
        yield return TestCase.Create(new ClassWithRequiredProperty { X = 42 });
        yield return TestCase.Create(new StructWithRequiredProperty { X = 42 });
        yield return TestCase.Create(new StructWithRequiredPropertyAndDefaultCtor { y = 2 });
        yield return TestCase.Create(new StructWithRequiredFieldAndDefaultCtor { y = 2 });

        yield return TestCase.Create(new ClassWithSetsRequiredMembersCtor(42));
        yield return TestCase.Create(new StructWithSetsRequiredMembersCtor(42));
        yield return TestCase.Create(new ClassWithSetsRequiredMembersDefaultCtor { Value = 42 });
        yield return TestCase.Create(new StructWithSetsRequiredMembersDefaultCtor { Value = 42 });

        yield return TestCase.Create(new ClassWithRequiredAndInitOnlyProperties
        {
            RequiredAndInitOnlyString = "str1",
            RequiredString = "str2",
            InitOnlyString = "str3",

            RequiredAndInitOnlyInt = 1,
            RequiredInt = 2,
            InitOnlyInt = 3,

            requiredField = true,
        });

        yield return TestCase.Create(new StructWithRequiredAndInitOnlyProperties
        {
            RequiredAndInitOnlyString = "str1",
            RequiredString = "str2",
            InitOnlyString = "str3",

            RequiredAndInitOnlyInt = 1,
            RequiredInt = 2,
            InitOnlyInt = 3,

            requiredField = true,
        });

        yield return TestCase.Create(new ClassRecordWithRequiredAndInitOnlyProperties(1, 2, 3)
        {
            RequiredAndInitOnlyString = "str1",
            RequiredString = "str2",
            InitOnlyString = "str3",

            RequiredAndInitOnlyInt = 1,
            RequiredInt = 2,
            InitOnlyInt = 3,

            requiredField = true,
        });

        yield return TestCase.Create(new StructRecordWithRequiredAndInitOnlyProperties(1, 2, 3)
        {
            RequiredAndInitOnlyString = "str1",
            RequiredString = "str2",
            InitOnlyString = "str3",

            RequiredAndInitOnlyInt = 1,
            RequiredInt = 2,
            InitOnlyInt = 3,

            requiredField = true,
        });

        yield return TestCase.Create(new ClassWithDefaultConstructorAndSingleRequiredProperty { Value = 42 });
        yield return TestCase.Create(new ClassWithParameterizedConstructorAnd2OptionalSetters(42));
        yield return TestCase.Create(new ClassWithParameterizedConstructorAnd10OptionalSetters(42));
        yield return TestCase.Create(new ClassWithParameterizedConstructorAnd70OptionalSetters(42));

        yield return TestCase.Create(new ClassRecord(0, 1, 2, 3));
        yield return TestCase.Create(new StructRecord(0, 1, 2, 3));
        yield return TestCase.Create(new LargeClassRecord());

        yield return TestCase.Create(new ClassWithIndexer());

        yield return TestCase.Create(new RecordWithDefaultParams());
        yield return TestCase.Create(new RecordWithDefaultParams2());

        yield return TestCase.Create(new RecordWithNullableDefaultParams());
        yield return TestCase.Create(new RecordWithNullableDefaultParams2());

        yield return TestCase.Create(new RecordWithSpecialValueDefaultParams(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0));

        yield return TestCase.Create(new RecordWithEnumAndNullableParams(MyEnum.A, MyEnum.C));
        yield return TestCase.Create(new RecordWithNullableDefaultEnum());

        yield return TestCase.Create(p, new GenericContainer<string>.Inner { Value = "str" });
        yield return TestCase.Create(p, new GenericContainer<string>.Inner<string> { Value1 = "str", Value2 = "str2" });

        yield return TestCase.Create(p, new MyLinkedList<int>
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
        
        yield return TestCase.Create<RecursiveClassWithNonNullableOccurrence>(null!);
        yield return TestCase.Create(new RecursiveClassWithNonNullableOccurrences
        {
            Values = [],
        });

        DateOnly today = DateOnly.Parse("2023-12-07");
        yield return TestCase.Create(new Todos(
            [ new (Id: 0, "Wash the dishes.", today, Status.Done),
              new (Id: 1, "Dry the dishes.", today, Status.Done),
              new (Id: 2, "Turn the dishes over.", today, Status.InProgress),
              new (Id: 3, "Walk the kangaroo.", today.AddDays(1), Status.NotStarted),
              new (Id: 4, "Call Grandma.", today.AddDays(1), Status.NotStarted)]));

        yield return TestCase.Create(new RecordWith21ConstructorParameters(
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2",
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2",
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2"));

        yield return TestCase.Create(new RecordWith42ConstructorParameters(
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2",
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2",
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2",
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2",
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2",
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2"));

        yield return TestCase.Create(new RecordWith42ConstructorParametersAndRequiredProperties(
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

        yield return TestCase.Create(new StructRecordWith42ConstructorParametersAndRequiredProperties(
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

        yield return TestCase.Create(new ClassWith40RequiredMembers
        {
            r00 = 00, r01 = 01, r02 = 02, r03 = 03, r04 = 04, r05 = 05, r06 = 06, r07 = 07, r08 = 08, r09 = 09, 
            r10 = 10, r11 = 11, r12 = 12, r13 = 13, r14 = 14, r15 = 15, r16 = 16, r17 = 17, r18 = 18, r19 = 19,
            r20 = 20, r21 = 21, r22 = 22, r23 = 23, r24 = 24, r25 = 25, r26 = 26, r27 = 27, r28 = 28, r29 = 29, 
            r30 = 30, r31 = 31, r32 = 32, r33 = 33, r34 = 34, r35 = 35, r36 = 36, r37 = 37, r38 = 38, r39 = 39,
        });

        yield return TestCase.Create(new StructWith40RequiredMembers
        {
            r00 = 00, r01 = 01, r02 = 02, r03 = 03, r04 = 04, r05 = 05, r06 = 06, r07 = 07, r08 = 08, r09 = 09, 
            r10 = 10, r11 = 11, r12 = 12, r13 = 13, r14 = 14, r15 = 15, r16 = 16, r17 = 17, r18 = 18, r19 = 19,
            r20 = 20, r21 = 21, r22 = 22, r23 = 23, r24 = 24, r25 = 25, r26 = 26, r27 = 27, r28 = 28, r29 = 29, 
            r30 = 30, r31 = 31, r32 = 32, r33 = 33, r34 = 34, r35 = 35, r36 = 36, r37 = 37, r38 = 38, r39 = 39,
        });

        yield return TestCase.Create(new StructWith40RequiredMembersAndDefaultCtor
        {
            r00 = 00, r01 = 01, r02 = 02, r03 = 03, r04 = 04, r05 = 05, r06 = 06, r07 = 07, r08 = 08, r09 = 09, 
            r10 = 10, r11 = 11, r12 = 12, r13 = 13, r14 = 14, r15 = 15, r16 = 16, r17 = 17, r18 = 18, r19 = 19,
            r20 = 20, r21 = 21, r22 = 22, r23 = 23, r24 = 24, r25 = 25, r26 = 26, r27 = 27, r28 = 28, r29 = 29, 
            r30 = 30, r31 = 31, r32 = 32, r33 = 33, r34 = 34, r35 = 35, r36 = 36, r37 = 37, r38 = 38, r39 = 39,
        });

        yield return TestCase.Create(new ClassWithInternalMembers { X = 1, Y = 2, Z = 3, W = 4, internalField = 5 }, isLossyRoundtrip: true);
        yield return TestCase.Create(new ClassWithPropertyAnnotations { X = 1, Y = 2, Z = true });
        yield return TestCase.Create(new ClassWithConstructorAndAnnotations(1, 2, true));
        yield return TestCase.Create(new DerivedClassWithPropertyShapeAnnotations());

        yield return TestCase.Create(new WeatherForecastDTO
        {
            Id = "id",
            Date = DateTime.Parse("1975-01-01"),
            DatesAvailable = [DateTime.Parse("1975-01-01"), DateTime.Parse("1976-01-01")],
            Summary = "Summary",
            SummaryField = "SummaryField",
            TemperatureCelsius = 42,
            SummaryWords = ["Summary", "Words"],
            TemperatureRanges = new()
            {
                ["Range1"] = new() { Low = 1, High = 2 },
                ["Range2"] = new() { Low = 3, High = 4 },
            }
        });
        
        yield return TestCase.Create(new DerivedClassWithShadowingMember { PropA = "propA", PropB = 2, FieldA = 1, FieldB = "fieldB" });
        yield return TestCase.Create(new ClassWithMultipleSelfReferences { First = new ClassWithMultipleSelfReferences() });
        yield return TestCase.Create(new ClassWithNullableTypeParameters());
        yield return TestCase.Create(p, new ClassWithNullableTypeParameters<int>());
        yield return TestCase.Create(p, new ClassWithNullableTypeParameters<int?>());
        yield return TestCase.Create(p, new ClassWithNullableTypeParameters<string>());
        yield return TestCase.Create(p, new CollectionWithNullableElement<int>([(1, 1)]));
        yield return TestCase.Create(p, new CollectionWithNullableElement<int?>([(null, 1), (42, 1)]));
        yield return TestCase.Create(p, new CollectionWithNullableElement<string?>([(null, 1), ("str", 2)]));
        yield return TestCase.Create(p, new DictionaryWithNullableEntries<int>([new("key1", (1, 1))]));
        yield return TestCase.Create(p, new DictionaryWithNullableEntries<int?>([new("key1", (null, 1)), new("key2", (42, 1))]));
        yield return TestCase.Create(p, new DictionaryWithNullableEntries<string>([new("key1", (null, 1)), new("key2", ("str", 1))]));
        yield return TestCase.Create(p, new ClassWithNullableProperty<int>());
        yield return TestCase.Create(p, new ClassWithNullableProperty<int?>());
        yield return TestCase.Create(p, new ClassWithNullableProperty<string>());

        yield return TestCase.Create(new PersonClass("John", 40));
        yield return TestCase.Create(new PersonStruct("John", 40));
        yield return TestCase.Create(p, (PersonStruct?)new PersonStruct("John", 40));
        yield return TestCase.Create<IPersonInterface>(new IPersonInterface.Impl("John", 40));
        yield return TestCase.Create<PersonAbstractClass>(new PersonAbstractClass.Impl("John", 40));
        yield return TestCase.Create(new PersonRecord("John", 40));
        yield return TestCase.Create(new PersonRecordStruct("John", 40));
        yield return TestCase.Create(p, (PersonRecordStruct?)new PersonRecordStruct("John", 40));
        yield return TestCase.Create(new ClassWithMultipleConstructors(z: 3) { X = 1, Y = 2 });
        yield return TestCase.Create(new ClassWithConflictingAnnotations
        {
            NonNullNullableString = new() { Value = "str" },
            NullableString = new() { Value = null },
        });

        yield return TestCase.Create(ClassWithRefConstructorParameter.Create(), hasRefConstructorParameters: true);
        yield return TestCase.Create(new ClassWithOutConstructorParameter(out _), hasRefConstructorParameters: true, hasOutConstructorParameters: true);
        yield return TestCase.Create(ClassWithMultipleRefConstructorParameters.Create(), hasRefConstructorParameters: true);

        // F# types
        yield return TestCase.Create(p, new FSharpRecord(42, "str", true));
        yield return TestCase.Create(p, new FSharpStructRecord(42, "str", true));
        yield return TestCase.Create(p, new GenericFSharpRecord<string>("str"));
        yield return TestCase.Create(p, new GenericFSharpStructRecord<string>("str"));
        yield return TestCase.Create(p, new FSharpClass("str", 42));
        yield return TestCase.Create(p, new FSharpStruct("str", 42));
        yield return TestCase.Create(p, new GenericFSharpClass<string>("str"));
        yield return TestCase.Create(p, new GenericFSharpStruct<string>("str"));
        yield return TestCase.Create(p, ListModule.OfSeq([1, 2, 3]));
        yield return TestCase.Create(p, SetModule.OfSeq([1, 2, 3]));
        yield return TestCase.Create(p, MapModule.OfSeq<string, int>([new("key1", 1), new("key2", 2)]));
        yield return TestCase.Create(p, FSharpRecordWithCollections.Create());
    }
}

[GenerateShape]
public partial class DerivedList : List<int>;

[GenerateShape]
public partial class DerivedDictionary : Dictionary<string, string>;

public readonly struct StructList<T> : IList<T>
{
    private readonly List<T> _values;
    public StructList() => _values = new();
    public T this[int index] { get => _values[index]; set => _values[index] = value; }
    public int Count => _values.Count;
    public bool IsReadOnly => false;
    public void Add(T item) => _values.Add(item);
    public void Clear() => _values.Clear();
    public bool Contains(T item) => _values.Contains(item);
    public void CopyTo(T[] array, int arrayIndex) => _values.CopyTo(array, arrayIndex);
    public int IndexOf(T item) => _values.IndexOf(item);
    public void Insert(int index, T item) => _values.Insert(index, item);
    public bool Remove(T item) => _values.Remove(item);
    public void RemoveAt(int index) => _values.RemoveAt(index);
    public IEnumerator<T> GetEnumerator() => _values.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _values.GetEnumerator();
}

public readonly struct StructDictionary<TKey, TValue> : IDictionary<TKey, TValue>
    where TKey : notnull
{
    private readonly Dictionary<TKey, TValue> _dictionary;
    public StructDictionary() => _dictionary = new();
    public TValue this[TKey key] { get => _dictionary[key]; set => _dictionary[key] = value; }
    public ICollection<TKey> Keys => _dictionary.Keys;
    public ICollection<TValue> Values => _dictionary.Values;
    public int Count => _dictionary.Count;
    public bool IsReadOnly => false;
    public void Add(TKey key, TValue value) => _dictionary.Add(key, value);
    public void Add(KeyValuePair<TKey, TValue> item) => _dictionary.Add(item.Key, item.Value);
    public void Clear() => _dictionary.Clear();
    public bool Contains(KeyValuePair<TKey, TValue> item) => _dictionary.Contains(item);
    public bool ContainsKey(TKey key) => _dictionary.ContainsKey(key);
    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) => ((IDictionary<TKey,TValue>)_dictionary).CopyTo(array, arrayIndex);
    public bool Remove(TKey key) => _dictionary.Remove(key);
    public bool Remove(KeyValuePair<TKey, TValue> item) => ((IDictionary<TKey, TValue>)_dictionary).Remove(item);
    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value) => _dictionary.TryGetValue(key, out value);
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => _dictionary.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _dictionary.GetEnumerator();
}

[GenerateShape]
public partial class PocoWithListAndDictionaryProps
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

internal class MyLinkedList<T>
{
    public T? Value { get; set; }
    public MyLinkedList<T>? Next { get; set; }
}

[GenerateShape]
internal partial class RecursiveClassWithNonNullableOccurrence
{
    public required RecursiveClassWithNonNullableOccurrence Value { get; init; }
}

[GenerateShape]
internal partial class RecursiveClassWithNonNullableOccurrences
{
    public required RecursiveClassWithNonNullableOccurrences[] Values { get; init; }
}

[GenerateShape]
public partial struct ComplexStruct
{
    public double real;
    public double im;
}

[GenerateShape]
public partial struct ComplexStructWithProperties
{
    public double Real { get; set; }
    public double Im { get; set; }
}

[GenerateShape]
public partial struct StructWithDefaultCtor
{
    public int Value;
    public StructWithDefaultCtor()
    {
        Value = 42;
    }
}

[GenerateShape]
public partial class BaseClass
{
    public int X { get; set; }
}

[GenerateShape]
public partial class DerivedClass : BaseClass
{
    public int Y { get; set; }
}

[GenerateShape]
public abstract partial class BaseClassWithVirtualProperties
{
    public virtual int X { get; set; }
    public abstract string Y { get; set; }
    public virtual int Z { get; set; }
    public virtual int W { get; set; }
}

[GenerateShape]
public partial class DerivedClassWithVirtualProperties : BaseClassWithVirtualProperties
{
    private int? _x;
    private string? _y;

    public override int X 
    {
        get => _x ?? 42;
        set
        {
            if (_x != null)
            {
                throw new InvalidOperationException("Value has already been set once");
            }

            _x = value;
        }
    }

    public override string Y
    {
        get => _y ?? "str";
        set
        {
            if (_y != null)
            {
                throw new InvalidOperationException("Value has already been set once");
            }

            _y = value;
        }
    }

    public override int Z => 42;
    public override int W { set => base.W = value; }
}

[GenerateShape]
public partial interface IBaseInterface
{
    public int X { get; set; }
}

[GenerateShape]
public partial interface IDerivedInterface : IBaseInterface
{
    public int Y { get; set; }
}

[GenerateShape]
public partial interface IDerived2Interface : IBaseInterface
{ 
    public int Z { get; set; }
}

[GenerateShape]
public partial interface IDerived3Interface : IBaseInterface
{
    public int W { get; set; }
}

[GenerateShape]
public partial interface IDiamondInterface : IDerivedInterface, IDerived2Interface, IDerived3Interface
{
    public int T { get; set; }
}

[GenerateShape]
public partial class DiamondImplementation : IDiamondInterface
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
    public int W { get; set; }
    public int T { get; set; }
}

[GenerateShape]
public partial class ClassWithRequiredField
{
    public required int x;
}

[GenerateShape]
public partial struct StructWithRequiredField
{
    public required int x;
}

[GenerateShape]
public partial class ClassWithRequiredProperty
{
    public required int X { get; set; }
}

[GenerateShape]
public partial struct StructWithRequiredProperty
{
    public required int X { get; set; }
}

[GenerateShape]
public partial class ClassWithReadOnlyField
{
    public readonly int field = 42;
}

[GenerateShape]
public partial struct StructWithRequiredPropertyAndDefaultCtor
{
    public StructWithRequiredPropertyAndDefaultCtor() { }
    public required int y { get; set; }
}

[GenerateShape]
public partial struct StructWithRequiredFieldAndDefaultCtor
{
    public StructWithRequiredFieldAndDefaultCtor() { }
    public required int y;
}

[GenerateShape]
public partial class ClassWithRequiredAndInitOnlyProperties
{
    public required string RequiredAndInitOnlyString { get; init; }
    public required string RequiredString { get; set; }
    public string? InitOnlyString { get; init; }

    public required int RequiredAndInitOnlyInt { get; init; }
    public required int RequiredInt { get; set; }
    public int InitOnlyInt { get; init; }

    public required bool requiredField;

}

[GenerateShape]
public partial struct StructWithRequiredAndInitOnlyProperties
{
    public required string RequiredAndInitOnlyString { get; init; }
    public required string RequiredString { get; set; }
    public string? InitOnlyString { get; init; }

    public required int RequiredAndInitOnlyInt { get; init; }
    public required int RequiredInt { get; set; }
    public int InitOnlyInt { get; init; }

    public required bool requiredField;
}

[GenerateShape]
public partial class ClassWithSetsRequiredMembersCtor
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

[GenerateShape]
public partial struct StructWithSetsRequiredMembersCtor
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

[GenerateShape]
public partial class ClassWithSetsRequiredMembersDefaultCtor
{
    [SetsRequiredMembers]
    public ClassWithSetsRequiredMembersDefaultCtor() { }

    public required int Value { get; set; }
}

[GenerateShape]
public partial struct StructWithSetsRequiredMembersDefaultCtor
{
    [SetsRequiredMembers]
    public StructWithSetsRequiredMembersDefaultCtor() { }

    public required int Value { get; set; }
}

public readonly struct GenericStructWithInitOnlyProperty<T>
{
    public T Value { get; init; } 
}

[GenerateShape]
public partial class ClassWithInitOnlyProperties
{
    public int Value { get; init; } = 42;
    public List<int> Values { get; init; } = [42];
}

[GenerateShape]
public partial class ClassWithIndexer
{
    public string this[int i]
    {
        get => i.ToString();
        set { }
    }
}

[GenerateShape]
public partial record ClassRecordWithRequiredAndInitOnlyProperties(int x, int y, int z)
{
    public required string RequiredAndInitOnlyString { get; init; }
    public required string RequiredString { get; set; }
    public string? InitOnlyString { get; init; }

    public required int RequiredAndInitOnlyInt { get; init; }
    public required int RequiredInt { get; set; }
    public int InitOnlyInt { get; init; }

    public required bool requiredField;
}

[GenerateShape]
public partial record struct StructRecordWithRequiredAndInitOnlyProperties(int x, int y, int z)
{
    public required string RequiredAndInitOnlyString { get; init; }
    public required string RequiredString { get; set; }
    public string? InitOnlyString { get; init; }

    public required int RequiredAndInitOnlyInt { get; init; }
    public required int RequiredInt { get; set; }
    public int InitOnlyInt { get; init; }

    public required bool requiredField;
}

[GenerateShape]
public partial class ClassWithDefaultConstructorAndSingleRequiredProperty
{
    public required int Value { get; set; }
}

[GenerateShape]
public partial class ClassWithParameterizedConstructorAnd2OptionalSetters(int x1)
{
    public int X1 { get; set; } = x1;
    public int X2 { get; set; }
}

[GenerateShape]
public partial class ClassWithParameterizedConstructorAnd10OptionalSetters(int x01)
{
    public int X01 { get; set; } = x01;
    public int X02 { get; set; } = x01;
    public int X03 { get; set; } = x01;
    public int X04 { get; set; } = x01;
    public int X05 { get; set; } = x01;
    public int X06 { get; set; } = x01;
    public int X07 { get; set; } = x01;
    public int X08 { get; set; } = x01;
    public int X09 { get; set; } = x01;
    public int X10 { get; set; } = x01;
}

[GenerateShape]
public partial class ClassWithParameterizedConstructorAnd70OptionalSetters(int x01)
{
    public int X01 { get; set; } = x01;
    public int X02 { get; set; } = x01;
    public int X03 { get; set; } = x01;
    public int X04 { get; set; } = x01;
    public int X05 { get; set; } = x01;
    public int X06 { get; set; } = x01;
    public int X07 { get; set; } = x01;
    public int X08 { get; set; } = x01;
    public int X09 { get; set; } = x01;
    public int X10 { get; set; } = x01;
    public int X11 { get; set; } = x01;
    public int X12 { get; set; } = x01;
    public int X13 { get; set; } = x01;
    public int X14 { get; set; } = x01;
    public int X15 { get; set; } = x01;
    public int X16 { get; set; } = x01;
    public int X17 { get; set; } = x01;
    public int X18 { get; set; } = x01;
    public int X19 { get; set; } = x01;
    public int X20 { get; set; } = x01;
    public int X21 { get; set; } = x01;
    public int X22 { get; set; } = x01;
    public int X23 { get; set; } = x01;
    public int X24 { get; set; } = x01;
    public int X25 { get; set; } = x01;
    public int X26 { get; set; } = x01;
    public int X27 { get; set; } = x01;
    public int X28 { get; set; } = x01;
    public int X29 { get; set; } = x01;
    public int X30 { get; set; } = x01;
    public int X31 { get; set; } = x01;
    public int X32 { get; set; } = x01;
    public int X33 { get; set; } = x01;
    public int X34 { get; set; } = x01;
    public int X35 { get; set; } = x01;
    public int X36 { get; set; } = x01;
    public int X37 { get; set; } = x01;
    public int X38 { get; set; } = x01;
    public int X39 { get; set; } = x01;
    public int X40 { get; set; } = x01;
    public int X41 { get; set; } = x01;
    public int X42 { get; set; } = x01;
    public int X43 { get; set; } = x01;
    public int X44 { get; set; } = x01;
    public int X45 { get; set; } = x01;
    public int X46 { get; set; } = x01;
    public int X47 { get; set; } = x01;
    public int X48 { get; set; } = x01;
    public int X49 { get; set; } = x01;
    public int X50 { get; set; } = x01;
    public int X51 { get; set; } = x01;
    public int X52 { get; set; } = x01;
    public int X53 { get; set; } = x01;
    public int X54 { get; set; } = x01;
    public int X55 { get; set; } = x01;
    public int X56 { get; set; } = x01;
    public int X57 { get; set; } = x01;
    public int X58 { get; set; } = x01;
    public int X59 { get; set; } = x01;
    public int X60 { get; set; } = x01;
    public int X61 { get; set; } = x01;
    public int X62 { get; set; } = x01;
    public int X63 { get; set; } = x01;
    public int X64 { get; set; } = x01;
    public int X65 { get; set; } = x01;
    public int X66 { get; set; } = x01;
    public int X67 { get; set; } = x01;
    public int X68 { get; set; } = x01;
    public int X69 { get; set; } = x01;
    public int X70 { get; set; } = x01;
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

[GenerateShape]
public partial class ClassWithNullabilityAttributes
{
    private string? _maybeNull = "str";
    private string? _allowNull = "str";
    private string? _notNull = "str";
    private string? _disallowNull = "str";

    public ClassWithNullabilityAttributes() { }

    public ClassWithNullabilityAttributes([AllowNull] string allowNull, [DisallowNull] string? disallowNull) 
    {
        _allowNull = allowNull;
        _disallowNull = disallowNull;
    }

    [MaybeNull]
    public string MaybeNullProperty
    {
        get => _maybeNull;
        set => _maybeNull = value;
    }

    [AllowNull]
    public string AllowNullProperty
    {
        get => _allowNull ?? "str";
        set => _allowNull = value;
    }

    [NotNull]
    public string? NotNullProperty
    {
        get => _notNull ?? "str";
        set => _notNull = value;
    }

    [DisallowNull]
    public string? DisallowNullProperty
    {
        get => _disallowNull;
        set => _disallowNull = value;
    }

    [MaybeNull]
    public string MaybeNullField = "str";
    [AllowNull]
    public string AllowNullField = "str";
    [NotNull]
    public string? NotNullField = "str";
    [DisallowNull]
    public string? DisallowNullField = "str";
}

public class ClassWithNullabilityAttributes<T>
{
    [NotNull]
    public T? NotNullProperty { get; set; }

    [DisallowNull]
    public T? DisallowNullProperty { get; set; }

    [NotNull]
    public required T NotNullField;

    [DisallowNull]
    public T? DisallowNullField;
}

public class ClassWithNotNullProperty<T> where T : notnull
{
    public required T Property { get; set; }
}

[GenerateShape]
public partial class ClassWithStructNullabilityAttributes
{
    private int? _maybeNull = 0;
    private int? _allowNull = 0;
    private int? _notNull = 0;
    private int? _disallowNull = 0;

    public ClassWithStructNullabilityAttributes() { }

    public ClassWithStructNullabilityAttributes([AllowNull] int? allowNull, [DisallowNull] int? disallowNull)
    {
        _allowNull = allowNull;
        _disallowNull = disallowNull;
    }

    [MaybeNull]
    public int? MaybeNullProperty
    {
        get => _maybeNull;
        set => _maybeNull = value;
    }

    [AllowNull]
    public int? AllowNullProperty
    {
        get => _allowNull ?? 0;
        set => _allowNull = value;
    }

    [NotNull]
    public int? NotNullProperty
    {
        get => _notNull ?? 0;
        set => _notNull = value;
    }

    [DisallowNull]
    public int? DisallowNullProperty
    {
        get => _disallowNull;
        set => _disallowNull = value;
    }

    [MaybeNull]
    public int MaybeNullField = 0;
    [AllowNull]
    public int AllowNullField = 0;
    [NotNull]
    public int? NotNullField = 0;
    [DisallowNull]
    public int? DisallowNullField = 0;
}

[GenerateShape]
public partial class ClassWithInternalConstructor
{
    [JsonConstructor, ConstructorShape]
    internal ClassWithInternalConstructor(int value) => Value = value;

    public int Value { get; }
}

[GenerateShape]
public partial record ParameterlessRecord();
[GenerateShape]
public partial record struct ParameterlessStructRecord();
[GenerateShape]
public partial record SimpleRecord(int value);
[GenerateShape]
public partial record NonNullStringRecord(string value);
[GenerateShape]
public partial record NullableStringRecord(string? value);
public record GenericRecord<T>(T value);
public readonly record struct GenericRecordStruct<T>(T value);
public record NotNullGenericRecord<T>(T value) where T : notnull;
public record NotNullClassGenericRecord<T>(T value) where T : class;
public record NullClassGenericRecord<T>(T value) where T : class?;
#nullable disable
public record NullObliviousGenericRecord<T>(T value);
#nullable restore

[GenerateShape]
public partial record ClassRecord(int x, int? y, int z, int w);
[GenerateShape]
public partial record struct StructRecord(int x, int y, int z, int w);

[GenerateShape]
public partial record RecordWithDefaultParams(bool x1 = true, byte x2 = 10, sbyte x3 = 10, char x4 = 'x', ushort x5 = 10, short x6 = 10, long x7 = 10);

[GenerateShape]
public partial record RecordWithDefaultParams2(ulong x1 = 10, float x2 = 3.1f, double x3 = 3.1d, decimal x4 = -3.1415926m, string x5 = "str", string? x6 = null, object? x7 = null);

[GenerateShape]
public partial record RecordWithNullableDefaultParams(bool? x1 = true, byte? x2 = 10, sbyte? x3 = 10, char? x4 = 'x', ushort? x5 = 10, short? x6 = 10, long? x7 = 10);

[GenerateShape]
public partial record RecordWithNullableDefaultParams2(ulong? x1 = 10, float? x2 = 3.1f, double? x3 = 3.1d, decimal? x4 = -3.1415926m, string? x5 = "str", string? x6 = null, object? x7 = null);

[GenerateShape]
public partial record RecordWithSpecialValueDefaultParams(
    double d1 = double.PositiveInfinity, double d2 = double.NegativeInfinity, double d3 = double.NaN,
    double? dn1 = double.PositiveInfinity, double? dn2 = double.NegativeInfinity, double? dn3 = double.NaN,
    float f1 = float.PositiveInfinity, float f2 = float.NegativeInfinity, float f3 = float.NaN,
    float? fn1 = float.PositiveInfinity, float? fn2 = float.NegativeInfinity, float? fn3 = float.NaN,
    string s = "\"😀葛🀄\r\n🤯𐐀𐐨\"", char c = '\'');

[Flags]
public enum MyEnum { A = 1, B = 2, C = 4, D = 8, E = 16, F = 32, G = 64, H = 128 }

[GenerateShape]
public partial record RecordWithEnumAndNullableParams(MyEnum flags1, MyEnum? flags2, MyEnum flags3 = MyEnum.A, MyEnum? flags4 = null);

[GenerateShape]
public partial record RecordWithNullableDefaultEnum(MyEnum? flags = MyEnum.A | MyEnum.B);

[GenerateShape]
public partial record LargeClassRecord(
    int x0 = 0, int x1 = 1, int x2 = 2, int x3 = 3, int x4 = 4, int x5 = 5, int x6 = 5, 
    int x7 = 7, int x8 = 8, string x9 = "str", LargeClassRecord? nested = null);

[GenerateShape]
public partial record RecordWith21ConstructorParameters(
    string x01, int x02, bool x03, TimeSpan x04, DateTime x05, int x06, string x07,
    string x08, int x09, bool x10, TimeSpan x11, DateTime x12, int x13, string x14,
    string x15, int x16, bool x17, TimeSpan x18, DateTime x19, int x20, string x21);

[GenerateShape]
public partial record RecordWith42ConstructorParameters(
    string x01, int x02, bool x03, TimeSpan x04, DateTime x05, int x06, string x07,
    string x08, int x09, bool x10, TimeSpan x11, DateTime x12, int x13, string x14,
    string x15, int x16, bool x17, TimeSpan x18, DateTime x19, int x20, string x21,
    string x22, int x23, bool x24, TimeSpan x25, DateTime x26, int x27, string x28,
    string x29, int x30, bool x31, TimeSpan x32, DateTime x33, int x34, string x35,
    string x36, int x37, bool x38, TimeSpan x39, DateTime x40, int x41, string x42);

[GenerateShape]
public partial record RecordWith42ConstructorParametersAndRequiredProperties(
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

[GenerateShape]
public partial record StructRecordWith42ConstructorParametersAndRequiredProperties(
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

[GenerateShape]
public partial struct ClassWith40RequiredMembers
{
    public required int r00; public required int r01; public required int r02; public required int r03; public required int r04; public required int r05; public required int r06; public required int r07; public required int r08; public required int r09;
    public required int r10; public required int r11; public required int r12; public required int r13; public required int r14; public required int r15; public required int r16; public required int r17; public required int r18; public required int r19;
    public required int r20; public required int r21; public required int r22; public required int r23; public required int r24; public required int r25; public required int r26; public required int r27; public required int r28; public required int r29;
    public required int r30; public required int r31; public required int r32; public required int r33; public required int r34; public required int r35; public required int r36; public required int r37; public required int r38; public required int r39;
}

[GenerateShape]
public partial struct StructWith40RequiredMembers
{
    public required int r00; public required int r01; public required int r02; public required int r03; public required int r04; public required int r05; public required int r06; public required int r07; public required int r08; public required int r09;
    public required int r10; public required int r11; public required int r12; public required int r13; public required int r14; public required int r15; public required int r16; public required int r17; public required int r18; public required int r19;
    public required int r20; public required int r21; public required int r22; public required int r23; public required int r24; public required int r25; public required int r26; public required int r27; public required int r28; public required int r29;
    public required int r30; public required int r31; public required int r32; public required int r33; public required int r34; public required int r35; public required int r36; public required int r37; public required int r38; public required int r39;
}

[GenerateShape]
public partial struct StructWith40RequiredMembersAndDefaultCtor
{
    public StructWith40RequiredMembersAndDefaultCtor() { }
    public required int r00; public required int r01; public required int r02; public required int r03; public required int r04; public required int r05; public required int r06; public required int r07; public required int r08; public required int r09;
    public required int r10; public required int r11; public required int r12; public required int r13; public required int r14; public required int r15; public required int r16; public required int r17; public required int r18; public required int r19;
    public required int r20; public required int r21; public required int r22; public required int r23; public required int r24; public required int r25; public required int r26; public required int r27; public required int r28; public required int r29;
    public required int r30; public required int r31; public required int r32; public required int r33; public required int r34; public required int r35; public required int r36; public required int r37; public required int r38; public required int r39;
}

[GenerateShape]
public partial class ClassWithInternalMembers
{
    public int X { get; set; }

    [PropertyShape(Ignore = false), JsonInclude]
    internal int Y { get; set; }
    [PropertyShape, JsonInclude]
    public int Z { internal get; set; }
    [PropertyShape, JsonInclude]
    public int W { get; internal set; }

    [PropertyShape, JsonInclude]
    internal int internalField;
}

[GenerateShape]
public partial class ClassWithPropertyAnnotations
{
    [PropertyShape(Name = "AltName", Order = 5)]
    [JsonPropertyName("AltName"), JsonPropertyOrder(5)]
    public int X { get; set; }

    [PropertyShape(Name = "AltName2", Order = -1)]
    [JsonPropertyName("AltName2"), JsonPropertyOrder(-1)]
    public int Y;

    [PropertyShape(Name = "Name\t\f\b with\r\nescaping\'\"", Order = 2)]
    [JsonPropertyName("Name\t\f\b with\r\nescaping\'\""), JsonPropertyOrder(2)]
    public bool Z;
}

[GenerateShape]
public partial class ClassWithConstructorAndAnnotations
{
    public ClassWithConstructorAndAnnotations(int x, [ParameterShape(Name = "AltName2")] int y, bool z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    [PropertyShape(Name = "AltName", Order = 5)]
    [JsonPropertyName("AltName"), JsonPropertyOrder(5)]
    public int X { get; }

    [PropertyShape(Name = "AltName2", Order = -1)]
    [JsonPropertyName("AltName2"), JsonPropertyOrder(-1)]
    public int Y { get; }

    [PropertyShape(Name = "Name\twith\r\nescaping", Order = 2)]
    [JsonPropertyName("Name\twith\r\nescaping"), JsonPropertyOrder(2)]
    public bool Z { get; }
}

[GenerateShape]
public abstract partial class BaseClassWithPropertyShapeAnnotations
{
    // JsonIgnore added because of a bug in the STJ baseline
    // cf. https://github.com/dotnet/runtime/issues/92780

    [PropertyShape(Name = "BaseX")]
    [JsonIgnore]
    public abstract int X { get; }

    [PropertyShape(Name = "BaseY")]
    [JsonIgnore]
    public virtual int Y { get; }

    [PropertyShape(Name = "BaseZ")]
    [JsonIgnore]
    public int Z { get; }
}

[GenerateShape]
public partial class DerivedClassWithPropertyShapeAnnotations : BaseClassWithPropertyShapeAnnotations
{
    [PropertyShape(Name = "DerivedX")]
    [JsonPropertyName("DerivedX")] // Expected name
    public override int X => 1;

    [JsonPropertyName("BaseY")] // Expected name
    public override int Y => 2;

    [PropertyShape(Name = "DerivedZ")]
    [JsonPropertyName("DerivedZ")] // Expected name
    public new int Z { get; } = 3;
}

[GenerateShape]
public partial class PersonClass(string name, int age)
{
    public string Name { get; } = name;
    public int Age { get; } = age;
}

[GenerateShape]
public partial struct PersonStruct(string name, int age)
{
    public string Name { get; } = name;
    public int Age { get; } = age;
}

[GenerateShape]
public partial interface IPersonInterface
{
    public string Name { get; }
    public int Age { get; }

    public class Impl(string name, int age) : IPersonInterface
    {
        public string Name { get; } = name;
        public int Age { get; } = age;
    }
}

[GenerateShape]
public abstract partial class PersonAbstractClass(string name, int age)
{
    public string Name { get; } = name;
    public int Age { get; } = age;

    public class Impl(string name, int age) : PersonAbstractClass(name, age);
}

[GenerateShape]
public partial record PersonRecord(string name, int age);

[GenerateShape]
public partial record struct PersonRecordStruct(string name, int age);

[GenerateShape]
[CollectionBuilder(typeof(CollectionWithBuilderAttribute), nameof(Create))]
public partial class CollectionWithBuilderAttribute : List<int>
{
    private CollectionWithBuilderAttribute() { }

    public static CollectionWithBuilderAttribute Create(ReadOnlySpan<int> values)
    {
        var result = new CollectionWithBuilderAttribute();
        foreach (var value in values)
        {
            result.Add(value);
        }
        return result;
    }
}

[CollectionBuilder(typeof(GenericCollectionWithBuilderAttribute), nameof(GenericCollectionWithBuilderAttribute.Create))]
public partial class GenericCollectionWithBuilderAttribute<T> : List<T>
{
    private GenericCollectionWithBuilderAttribute() { }

    public static GenericCollectionWithBuilderAttribute<T> CreateEmpty()
        => new GenericCollectionWithBuilderAttribute<T>();
}

public static class GenericCollectionWithBuilderAttribute
{
    public static GenericCollectionWithBuilderAttribute<T> Create<T>(ReadOnlySpan<T> values)
    {
        var result = GenericCollectionWithBuilderAttribute<T>.CreateEmpty();
        foreach (var value in values)
        {
            result.Add(value);
        }
        return result;
    }
}

[GenerateShape]
public partial class CollectionWithEnumerableCtor : List<int>
{
    public CollectionWithEnumerableCtor(IEnumerable<int> values)
    {
        foreach (var value in values)
        {
            Add(value);
        }
    }
}

[GenerateShape]
public partial class DictionaryWithEnumerableCtor : Dictionary<string, int>
{
    public DictionaryWithEnumerableCtor(IEnumerable<KeyValuePair<string, int>> values)
    {
        foreach (var value in values)
        {
            this[value.Key] = value.Value;
        }
    }
}

[GenerateShape]
public partial class CollectionWithSpanCtor : List<int>
{
    public CollectionWithSpanCtor(ReadOnlySpan<int> values)
    {
        foreach (var value in values)
        {
            Add(value);
        }
    }
}

[GenerateShape]
public partial class DictionaryWithSpanCtor : Dictionary<string, int>
{
    public DictionaryWithSpanCtor(ReadOnlySpan<KeyValuePair<string, int>> values)
    {
        foreach (var value in values)
        {
            this[value.Key] = value.Value;
        }
    }
}

public class MyKeyedCollection<T> : KeyedCollection<int, T>
{
    private int _count;
    protected override int GetKeyForItem(T key) => _count++;
}

[GenerateShape]
public partial record Todos(Todo[] Items);

[GenerateShape]
public partial record Todo(int Id, string? Title, DateOnly? DueBy, Status Status);

public enum Status { NotStarted, InProgress, Done }

[GenerateShape]
public partial class WeatherForecastDTO
{
    public required string Id { get; set; }
    public DateTimeOffset Date { get; set; }
    public int TemperatureCelsius { get; set; }
    public string? Summary { get; set; }
    public string? SummaryField;
    public List<DateTimeOffset>? DatesAvailable { get; set; }
    public Dictionary<string, HighLowTempsDTO>? TemperatureRanges { get; set; }
    public string[]? SummaryWords { get; set; }
}

public class HighLowTempsDTO
{
    public int High { get; set; }
    public int Low { get; set; }
}

[GenerateShape]
public partial class WeatherForecast
{
    public DateTimeOffset Date { get; init; }
    public int TemperatureCelsius { get; init; }
    public IReadOnlyList<DateTimeOffset>? DatesAvailable { get; init; }
    public IReadOnlyDictionary<string, HighLowTemps>? TemperatureRanges { get; init; }
    public IReadOnlyList<string>? SummaryWords { get; init; }
    public string? UnmatchedProperty { get; init; }
}

public record HighLowTemps
{
    public int High { get; init; }
}

[GenerateShape]
public partial record BaseClassWithShadowingMembers
{
    public string? PropA { get; init; }
    public string? PropB { get; init; }
    public int FieldA;
    public int FieldB;
}

[GenerateShape]
public partial record DerivedClassWithShadowingMember : BaseClassWithShadowingMembers
{
    public new string? PropA { get; init; }
    public required new int PropB { get; init; }
    public new int FieldA;
    public required new string FieldB;
}

[GenerateShape]
public partial class ClassWithMultipleSelfReferences
{
    public long Id { get; set; }
    public ClassWithMultipleSelfReferences? First { get; set; }
    public ClassWithMultipleSelfReferences[] FirstArray { get; set; } = [];
}

[GenerateShape]
public partial class ClassWithNullableTypeParameters
{
    public string?[] DataArray { get; set; } = [null, "str"];
    public List<string?> DataList { get; set; } = [null, "str"];
    public List<string?> InitOnlyDataList { get; init; } = [null, "str"];
    public Dictionary<int, string?[]> DataDict { get; set; } = new() { [0] = [null, "str"] };
}

public class ClassWithNullableTypeParameters<T>
{
    public T?[] DataArray { get; set; } = [default];
    public List<T?> DataList { get; set; } = [default];
    public List<T?> InitOnlyDataList { get; init; } = [default];
    public Dictionary<int, T?[]> DataDict { get; set; } = new() { [0] = [default] };
}

public class CollectionWithNullableElement<T>(IEnumerable<(T?, int)> values) : IEnumerable<(T?, int)>
{
    private readonly (T?, int)[] _values = values.ToArray();
    public IEnumerator<(T?, int)> GetEnumerator() => ((IEnumerable<(T?, int)>)_values).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _values.GetEnumerator();
}

public class DictionaryWithNullableEntries<T>(IEnumerable<KeyValuePair<string, (T?, int)>> values) : IReadOnlyDictionary<string, (T?, int)>
{
    private readonly Dictionary<string, (T?, int)> _source = new(values);
    public IEnumerator<KeyValuePair<string, (T?, int)>> GetEnumerator() => _source.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _source.GetEnumerator();
    public int Count => _source.Count;
    public bool ContainsKey(string key) => _source.ContainsKey(key);
    public bool TryGetValue(string key, out (T?, int) value) => _source.TryGetValue(key, out value);
    public (T?, int) this[string key] => _source[key];
    public IEnumerable<string> Keys => _source.Keys;
    public IEnumerable<(T?, int)> Values => _source.Values;
}

public class ClassWithNullableProperty<T>
{
    public (int, T?)? Value { get; set; }
}

[GenerateShape]
partial class ClassWithMultipleConstructors
{
    public ClassWithMultipleConstructors(int x, int y)
    {
        X = x;
        Y = y;
    }

    [JsonConstructor]
    public ClassWithMultipleConstructors(int z)
    {
        // TypeShape should automatically pick this ctor
        // as it maximizes the possible properties that get initialized.

        Z = z;
    }
    
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; }
}

[GenerateShape]
public partial class ClassWithConflictingAnnotations
{
    public required GenericClass<string?> NullableString { get; set; }
    public required GenericClass<string> NonNullNullableString { get; set; }

    public class GenericClass<T>
    {
        public required T Value { get; set; }
    }
}

[GenerateShape]
public partial class ClassWithRefConstructorParameter(ref int value)
{
    public int Value { get; } = value;

    public static ClassWithRefConstructorParameter Create()
    {
        int value = 42;
        return new ClassWithRefConstructorParameter(ref value);
    }
}

[GenerateShape]
public partial class ClassWithOutConstructorParameter
{
    public ClassWithOutConstructorParameter(out int value)
    {
        Value = value = 42;
    }
    
    public int Value { get; }
}

[GenerateShape]
public partial class ClassWithMultipleRefConstructorParameters
{
    public ClassWithMultipleRefConstructorParameters(ref int intValue, in bool boolValue, ref readonly DateTime dateValue)
    {
        IntValue = intValue;
        BoolValue = boolValue;
        DateValue = dateValue;
    }
    
    public int IntValue { get; }
    public bool BoolValue { get; }
    public DateTime DateValue { get; }

    public static ClassWithMultipleRefConstructorParameters Create()
    {
        int intValue = 42;
        bool boolValue = true;
        DateTime dateValue = DateTime.MaxValue;
        return new ClassWithMultipleRefConstructorParameters(ref intValue, in boolValue, ref dateValue);
    }
}

[GenerateShape<object>]
[GenerateShape<bool>]
[GenerateShape<char>]
[GenerateShape<string>]
[GenerateShape<sbyte>]
[GenerateShape<short>]
[GenerateShape<int>]
[GenerateShape<long>]
[GenerateShape<byte>]
[GenerateShape<ushort>]
[GenerateShape<uint>]
[GenerateShape<ulong>]
[GenerateShape<float>]
[GenerateShape<double>]
[GenerateShape<decimal>]
[GenerateShape<Half>]
[GenerateShape<Int128>]
[GenerateShape<UInt128>]
[GenerateShape<Rune>]
[GenerateShape<Guid>]
[GenerateShape<DateTime>]
[GenerateShape<DateTimeOffset>]
[GenerateShape<TimeSpan>]
[GenerateShape<DateOnly>]
[GenerateShape<TimeOnly>]
[GenerateShape<BigInteger>]
[GenerateShape<BindingFlags>]
[GenerateShape<MyEnum>]
[GenerateShape<bool?>]
[GenerateShape<sbyte?>]
[GenerateShape<short?>]
[GenerateShape<int?>]
[GenerateShape<long?>]
[GenerateShape<byte?>]
[GenerateShape<ushort?>]
[GenerateShape<uint?>]
[GenerateShape<ulong?>]
[GenerateShape<float?>]
[GenerateShape<double?>]
[GenerateShape<decimal?>]
[GenerateShape<Half?>]
[GenerateShape<Int128?>]
[GenerateShape<UInt128?>]
[GenerateShape<Rune?>]
[GenerateShape<Guid?>]
[GenerateShape<DateTime?>]
[GenerateShape<DateTimeOffset?>]
[GenerateShape<TimeSpan?>]
[GenerateShape<DateOnly?>]
[GenerateShape<TimeOnly?>]
[GenerateShape<BigInteger?>]
[GenerateShape<BindingFlags?>]
[GenerateShape<Uri>]
[GenerateShape<Version>]
[GenerateShape<string[]>]
[GenerateShape<byte[]>]
[GenerateShape<int[]>]
[GenerateShape<int[][]>]
[GenerateShape<int[,]>]
[GenerateShape<int[,,]>]
[GenerateShape<int[,,,,,]>]
[GenerateShape<Memory<int>>]
[GenerateShape<ReadOnlyMemory<int>>]
[GenerateShape<List<string>>]
[GenerateShape<List<byte>>]
[GenerateShape<List<int>>]
[GenerateShape<LinkedList<byte>>]
[GenerateShape<Stack<int>>]
[GenerateShape<Queue<int>>]
[GenerateShape<Dictionary<string, int>>]
[GenerateShape<Dictionary<string, string>>]
[GenerateShape<Dictionary<SimpleRecord, string>>]
[GenerateShape<Dictionary<string, SimpleRecord>>]
[GenerateShape<SortedSet<string>>]
[GenerateShape<SortedDictionary<string, int>>]
[GenerateShape<ConcurrentStack<int>>]
[GenerateShape<ConcurrentQueue<int>>]
[GenerateShape<ConcurrentDictionary<string, string>>]
[GenerateShape<HashSet<string>>]
[GenerateShape<Hashtable>]
[GenerateShape<ArrayList>]
[GenerateShape<StructList<int>>]
[GenerateShape<StructDictionary<string, string>>]
[GenerateShape<GenericRecord<int>>]
[GenerateShape<GenericRecord<string>>]
[GenerateShape<GenericRecord<GenericRecord<bool>>>]
[GenerateShape<GenericRecord<GenericRecord<int>>>]
[GenerateShape<GenericRecordStruct<int>>]
[GenerateShape<GenericRecordStruct<string>>]
[GenerateShape<GenericRecordStruct<GenericRecordStruct<bool>>>]
[GenerateShape<GenericRecordStruct<GenericRecordStruct<int>>>]
[GenerateShape<GenericStructWithInitOnlyProperty<int>>]
[GenerateShape<GenericStructWithInitOnlyProperty<string>>]
[GenerateShape<GenericStructWithInitOnlyProperty<GenericStructWithInitOnlyProperty<string>>>]
[GenerateShape<ImmutableArray<int>>]
[GenerateShape<ImmutableList<string>>]
[GenerateShape<ImmutableQueue<int>>]
[GenerateShape<ImmutableStack<int>>]
[GenerateShape<ImmutableHashSet<int>>]
[GenerateShape<ImmutableSortedSet<int>>]
[GenerateShape<ImmutableDictionary<string, string>>]
[GenerateShape<ImmutableSortedDictionary<string, string>>]
[GenerateShape<IEnumerable>]
[GenerateShape<IList>]
[GenerateShape<ICollection>]
[GenerateShape<IDictionary>]
[GenerateShape<IEnumerable<int>>]
[GenerateShape<ICollection<int>>]
[GenerateShape<IList<int>>]
[GenerateShape<IReadOnlyCollection<int>>]
[GenerateShape<IReadOnlyList<int>>]
[GenerateShape<ISet<int>>]
[GenerateShape<IReadOnlySet<int>>]
[GenerateShape<IDictionary<int, int>>]
[GenerateShape<IReadOnlyDictionary<int, int>>]
[GenerateShape<NotNullGenericRecord<string>>]
[GenerateShape<NotNullClassGenericRecord<string>>]
[GenerateShape<NullClassGenericRecord<string>>]
[GenerateShape<NullObliviousGenericRecord<string>>]
[GenerateShape<MyLinkedList<int>>]
[GenerateShape<RecursiveClassWithNonNullableOccurrence>]
[GenerateShape<RecursiveClassWithNonNullableOccurrences>]
[GenerateShape<GenericContainer<string>.Inner>]
[GenerateShape<GenericContainer<string>.Inner<string>>]
[GenerateShape<ValueTuple>]
[GenerateShape<ValueTuple<int>>]
[GenerateShape<ValueTuple<int, string>>]
[GenerateShape<ValueTuple<int, int, int, int, int, int, int, int>>]
[GenerateShape<(int, string)>]
[GenerateShape<(int, string, bool)>]
[GenerateShape<(int, string, (int, int))>]
[GenerateShape<(int, int, int, int, int, int, int)>]
[GenerateShape<(int, int, int, int, int, int, int, int, int)>]
[GenerateShape<(int, int, int, int, int, int, int, int, int, int,
    int, int, int, int, int, int, int, int, int, int,
    int, int, int, int, int, int, int, int, int, int)>]
[GenerateShape<Dictionary<int, (int, int)>>]
[GenerateShape<Tuple<int>>]
[GenerateShape<Tuple<int, int>>]
[GenerateShape<Tuple<int, string, bool>>]
[GenerateShape<Tuple<int, int, int, int, int, int, int>>]
[GenerateShape<Tuple<int, int, int, int, int, int, int, int>>]
[GenerateShape<Tuple<int, int, int, int, int, int, int, Tuple<int, int, int>>>]
[GenerateShape<Tuple<int, int, int, int, int, int, int, Tuple<int, int, int, int, int, int, int, Tuple<int>>>>]
[GenerateShape<MyLinkedList<SimpleRecord>>]
[GenerateShape<PersonStruct?>]
[GenerateShape<PersonRecordStruct?>]
[GenerateShape<GenericCollectionWithBuilderAttribute<int>>]
[GenerateShape<ReadOnlyCollection<int>>]
[GenerateShape<Collection<int>>]
[GenerateShape<ReadOnlyCollection<int>>]
[GenerateShape<ReadOnlyDictionary<int, int>>]
[GenerateShape<ObservableCollection<int>>]
[GenerateShape<MyKeyedCollection<int>>]
[GenerateShape<MyKeyedCollection<string>>]
[GenerateShape<ClassWithNullableTypeParameters<int>>]
[GenerateShape<ClassWithNullableTypeParameters<int?>>]
[GenerateShape<ClassWithNullableTypeParameters<string>>]
[GenerateShape<CollectionWithNullableElement<int>>]
[GenerateShape<CollectionWithNullableElement<int?>>]
[GenerateShape<CollectionWithNullableElement<string>>]
[GenerateShape<DictionaryWithNullableEntries<int>>]
[GenerateShape<DictionaryWithNullableEntries<int?>>]
[GenerateShape<DictionaryWithNullableEntries<string>>]
[GenerateShape<ClassWithNullableProperty<int>>]
[GenerateShape<ClassWithNullableProperty<int?>>]
[GenerateShape<ClassWithNullableProperty<string>>]
[GenerateShape<ClassWithNullabilityAttributes<string>>]
[GenerateShape<ClassWithNotNullProperty<string>>]
[GenerateShape<FSharpRecord>]
[GenerateShape<FSharpStructRecord>]
[GenerateShape<GenericFSharpRecord<string>>]
[GenerateShape<GenericFSharpStructRecord<string>>]
[GenerateShape<FSharpClass>]
[GenerateShape<FSharpStruct>]
[GenerateShape<GenericFSharpClass<string>>]
[GenerateShape<GenericFSharpStruct<string>>]
[GenerateShape<FSharpList<int>>]
[GenerateShape<FSharpMap<string, int>>]
[GenerateShape<FSharpSet<int>>]
[GenerateShape<FSharpRecordWithCollections>]
internal partial class SourceGenProvider;

internal partial class Outer1
{
    public partial class Outer2
    {
        [GenerateShape<int>]
        [GenerateShape<Private>]
        private partial class Nested { }

        private class Private { }
    }
}