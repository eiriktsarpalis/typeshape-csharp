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

public sealed record TestCase<T, TProvider>(T Value) : TestCase<T>(Value)
    where TProvider : ITypeShapeProvider<T>;

public abstract record TestCase<T>(T Value) : ITestCase
{
    Type ITestCase.Type => typeof(T);
    object? ITestCase.Value => Value;
    public bool HasConstructors => 
        !(IsAbstract && !typeof(IEnumerable).IsAssignableFrom(typeof(T))) &&
        !IsMultiDimensionalArray;

    public bool IsNullable => default(T) is null;
    public bool IsEquatable => Value is IEquatable<T> &&
        !typeof(T).IsImmutableArray() &&
        !typeof(T).IsMemoryType(out _, out _) &&
        !typeof(T).IsRecord();

    public bool IsTuple => Value is ITuple;
    public bool IsLongTuple => Value is ITuple { Length: > 7 };
    public bool IsMultiDimensionalArray => typeof(T).IsArray && typeof(T).GetArrayRank() != 1;
    public bool IsAbstract => typeof(T).IsAbstract || typeof(T).IsInterface;
    public bool IsStack { get; init; }
    public bool DoesNotRoundtrip { get; init; }
}

public interface ITestCase
{
    public Type Type { get; }
    public object? Value { get; }
    public bool HasConstructors { get; }
}

public static class TestTypes
{

    public static IEnumerable<object[]> GetTestCases()
        => GetTestCasesCore().Select(value => new object[] { value });

    public static IEnumerable<ITestCase> GetTestCasesCore()
    {
        SourceGenProvider p = SourceGenProvider.Default;
        yield return Create(new object(), p);
        yield return Create(false, p);
        yield return Create("", p);
        yield return Create("stringValue", p);
        yield return Create(Rune.GetRuneAt("🤯", 0), p);
        yield return Create(sbyte.MinValue, p);
        yield return Create(short.MinValue, p);
        yield return Create(int.MinValue, p);
        yield return Create(long.MinValue, p);
        yield return Create(byte.MaxValue, p);
        yield return Create(ushort.MaxValue, p);
        yield return Create(uint.MaxValue, p);
        yield return Create(ulong.MaxValue, p);
        yield return Create(Int128.MaxValue, p);
        yield return Create(UInt128.MaxValue, p);
        yield return Create(BigInteger.Parse("-170141183460469231731687303715884105728"), p);
        yield return Create(3.14f, p);
        yield return Create(3.14d, p);
        yield return Create(3.14M, p);
        yield return Create((Half)3.14, p);
        yield return Create(Guid.Empty, p);
        yield return Create(DateTime.MaxValue, p);
        yield return Create(DateTimeOffset.MaxValue, p);
        yield return Create(TimeSpan.MaxValue, p);
        yield return Create(DateOnly.MaxValue, p);
        yield return Create(TimeOnly.MaxValue, p);
        yield return Create(new Uri("https://github.com"), p);
        yield return Create(new Version("1.0.0.0"), p);
        yield return Create(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, p);

        yield return Create(new int[0], p);
        yield return Create<int[], SourceGenProvider>([1, 2, 3], p);
        yield return Create<int[][], SourceGenProvider>([[1, 0, 0], [0, 1, 0], [0, 0, 1]], p);
        yield return Create<byte[], SourceGenProvider>([1, 2, 3], p);
        yield return Create<Memory<int>, SourceGenProvider>(new int[] { 1, 2, 3 }, p);
        yield return Create<ReadOnlyMemory<int>, SourceGenProvider>(new[] { 1, 2, 3 }, p);
        yield return Create<List<string>, SourceGenProvider>(["1", "2", "3"], p);
        yield return Create<List<byte>, SourceGenProvider>([], p);
        yield return Create<LinkedList<byte>, SourceGenProvider>([], p);
        yield return Create(new Queue<int>([1, 2, 3]), p);
        yield return Create(new Stack<int>([1, 2, 3]), p, isStack: true);
        yield return Create(new Dictionary<string, int> { ["key1"] = 42, ["key2"] = -1 }, p);
        yield return Create<HashSet<string>, SourceGenProvider>(["apple", "orange", "banana"], p);
        yield return Create<HashSet<string>, SourceGenProvider>(["apple", "orange", "banana"], p);
        yield return Create<SortedSet<string>, SourceGenProvider>(["apple", "orange", "banana"], p);
        yield return Create(new SortedDictionary<string, int> { ["key1"] = 42, ["key2"] = -1 }, p);

        yield return Create(new Hashtable { ["key1"] = 42 }, p);
        yield return Create<ArrayList, SourceGenProvider>([1, 2, 3], p);

        yield return Create(new int[,] { { 1, 0, 0 }, { 0, 1, 0 }, { 0, 0, 1 } }, p);
        yield return Create(new int[,,] { { { 1 } } }, p);

        yield return Create(new ConcurrentQueue<int>([1, 2, 3]), p);
        yield return Create(new ConcurrentStack<int>([1, 2, 3]), p, isStack: true);
        yield return Create(new ConcurrentDictionary<string, string> { ["key"] = "value" }, p);

        yield return Create<IEnumerable, SourceGenProvider>(new List<object> { 1, 2, 3 }, p);
        yield return Create<IList, SourceGenProvider>(new List<object> { 1, 2, 3 }, p);
        yield return Create<ICollection, SourceGenProvider>(new List<object> { 1, 2, 3 }, p);
        yield return Create<IDictionary, SourceGenProvider>(new Dictionary<object, object> { [42] = 42 }, p);
        yield return Create<IEnumerable<int>, SourceGenProvider>([1, 2, 3], p);
        yield return Create<ICollection<int>, SourceGenProvider>([1, 2, 3], p);
        yield return Create<IList<int>, SourceGenProvider>([1, 2, 3], p);
        yield return Create<IReadOnlyCollection<int>, SourceGenProvider>([1, 2, 3], p);
        yield return Create<IReadOnlyList<int>, SourceGenProvider>([1, 2, 3], p);
        yield return Create<ISet<int>, SourceGenProvider>(new HashSet<int> { 1, 2, 3 }, p);
        yield return Create<IReadOnlySet<int>, SourceGenProvider>(new HashSet<int> { 1, 2, 3 }, p);
        yield return Create<IDictionary<int, int>, SourceGenProvider>(new Dictionary<int, int> { [42] = 42 }, p);
        yield return Create<IReadOnlyDictionary<int, int>, SourceGenProvider>(new Dictionary<int, int> { [42] = 42 }, p);

        yield return Create<DerivedList, SourceGenProvider>([1, 2, 3], p);
        yield return Create(new DerivedDictionary { ["key"] = "value" }, p);

        yield return Create<StructList<int>, SourceGenProvider>([1, 2, 3], p);
        yield return Create(new StructDictionary<string, string> { ["key"] = "value" }, p);
        yield return Create<CollectionWithBuilderAttribute, SourceGenProvider>([1, 2, 3], p);
        yield return Create<GenericCollectionWithBuilderAttribute<int>, SourceGenProvider>([1, 2, 3], p);

        yield return Create<ImmutableArray<int>, SourceGenProvider>([1, 2, 3], p);
        yield return Create<ImmutableList<string>, SourceGenProvider>(["1", "2", "3"], p);
        yield return Create<ImmutableList<string?>, SourceGenProvider>(["1", "2", null], p);
        yield return Create<ImmutableQueue<int>, SourceGenProvider>([1, 2, 3], p);
        yield return Create<ImmutableStack<int>, SourceGenProvider>([1, 2, 3], p, isStack: true);
        yield return Create<ImmutableHashSet<int>, SourceGenProvider>([1, 2, 3], p);
        yield return Create<ImmutableSortedSet<int>, SourceGenProvider>([1, 2, 3], p);
        yield return Create(ImmutableDictionary.CreateRange(new Dictionary<string, string> { ["key"] = "value" }), p);
        yield return Create(ImmutableDictionary.CreateRange(new Dictionary<string, string?> { ["key"] = null }), p);
        yield return Create(ImmutableSortedDictionary.CreateRange(new Dictionary<string, string> { ["key"] = "value" }), p);

        yield return Create(new PocoWithListAndDictionaryProps(@string: "myString")
        {
            List = [1, 2, 3],
            Dict = new() { ["key1"] = 42, ["key2"] = -1 },
        }, p);

        yield return Create(new BaseClass { X = 1 }, p);
        yield return Create(new DerivedClass { X = 1, Y = 2 }, p);

        var value = new DiamondImplementation { X = 1, Y = 2, Z = 3, W = 4, T = 5 };
        yield return Create<IBaseInterface, SourceGenProvider>(value, p);
        yield return Create<IDerivedInterface, SourceGenProvider>(value, p);
        yield return Create<IDerived2Interface, SourceGenProvider>(value, p);
        yield return Create<IDerived3Interface, SourceGenProvider>(value, p);
        yield return Create<IDiamondInterface, SourceGenProvider>(value, p);

        yield return Create(new ParameterlessRecord(), p);
        yield return Create(new ParameterlessStructRecord(), p);

        yield return Create(new ClassWithNullabilityAttributes(), p);
        yield return Create(new ClassWithStructNullabilityAttributes(), p);
        yield return Create(new NonNullStringRecord("str"), p);
        yield return Create(new NullableStringRecord(null), p);
        yield return Create(new NotNullGenericRecord<string>("str"), p);
        yield return Create(new NotNullClassGenericRecord<string>("str"), p);
        yield return Create(new NullClassGenericRecord<string>("str"), p);
        yield return Create(new NullObliviousGenericRecord<string>("str"), p);

        yield return Create(new SimpleRecord(42), p);
        yield return Create(new GenericRecord<int>(42), p);
        yield return Create(new GenericRecord<string>("str"), p);
        yield return Create(new GenericRecord<GenericRecord<bool>>(new GenericRecord<bool>(true)), p);

        yield return Create(new ComplexStruct { real = 0, im = 1 }, p);
        yield return Create(new ComplexStructWithProperties { Real = 0, Im = 1 }, p);
        yield return Create(new StructWithDefaultCtor(), p);

        yield return Create(new ValueTuple(), p);
        yield return Create(new ValueTuple<int>(42), p);
        yield return Create((42, "string"), p);
        yield return Create((1, 2, 3, 4, 5, 6, 7), p);
        yield return Create((IntValue: 42, StringValue: "string", BoolValue: true), p);
        yield return Create((IntValue: 42, StringValue: "string", (1, 0)), p);
        yield return Create((x1: 1, x2: 2, x3: 3, x4: 4, x5: 5, x6: 6, x7: 7, x8: 8, x9: 9), p);
        yield return Create((x01: 01, x02: 02, x03: 03, x04: 04, x05: 05, x06: 06, x07: 07, x08: 08, x09: 09, x10: 10,
                             x11: 11, x12: 12, x13: 13, x14: 14, x15: 15, x16: 16, x17: 17, x18: 18, x19: 19, x20: 20,
                             x21: 21, x22: 22, x23: 23, x24: 24, x25: 25, x26: 26, x27: 27, x28: 28, x29: 29, x30: 30), p);

        yield return Create(new Dictionary<int, (int, int)> { [0] = (1, 1) }, p);

        yield return Create<Tuple<int>, SourceGenProvider>(new(1), p);
        yield return Create<Tuple<int, int>, SourceGenProvider>(new(1, 2), p);
        yield return Create<Tuple<int, string, bool>, SourceGenProvider>(new(1, "str", true), p);
        yield return Create<Tuple<int, int, int, int, int, int, int>, SourceGenProvider>(new(1, 2, 3, 4, 5, 6, 7), p);
        yield return Create<Tuple<int, int, int, int, int, int, int, Tuple<int, int, int>>, SourceGenProvider>(new(1, 2, 3, 4, 5, 6, 7, new(8, 9, 10)), p);
        yield return Create<Tuple<int, int, int, int, int, int, int, Tuple<int, int, int, int, int, int, int, Tuple<int>>>, SourceGenProvider>(new(1, 2, 3, 4, 5, 6, 7, new(8, 9, 10, 11, 12, 13, 14, new(15))), p);

        yield return Create(new ClassWithReadOnlyField(), p);
        yield return Create(new ClassWithRequiredField { x = 42 }, p);
        yield return Create(new StructWithRequiredField { x = 42 }, p);
        yield return Create(new ClassWithRequiredProperty { X = 42 }, p);
        yield return Create(new StructWithRequiredProperty { X = 42 }, p);
        yield return Create(new StructWithRequiredPropertyAndDefaultCtor { y = 2 }, p);
        yield return Create(new StructWithRequiredFieldAndDefaultCtor { y = 2 }, p);

        yield return Create(new ClassWithSetsRequiredMembersCtor(42), p);
        yield return Create(new StructWithSetsRequiredMembersCtor(42), p);

        yield return Create(new ClassWithRequiredAndInitOnlyProperties
        {
            RequiredAndInitOnlyString = "str1",
            RequiredString = "str2",
            InitOnlyString = "str3",

            RequiredAndInitOnlyInt = 1,
            RequiredInt = 2,
            InitOnlyInt = 3,

            requiredField = true,
        }, p);

        yield return Create(new StructWithRequiredAndInitOnlyProperties
        {
            RequiredAndInitOnlyString = "str1",
            RequiredString = "str2",
            InitOnlyString = "str3",

            RequiredAndInitOnlyInt = 1,
            RequiredInt = 2,
            InitOnlyInt = 3,

            requiredField = true,
        }, p);

        yield return Create(new ClassRecordWithRequiredAndInitOnlyProperties(1, 2, 3)
        {
            RequiredAndInitOnlyString = "str1",
            RequiredString = "str2",
            InitOnlyString = "str3",

            RequiredAndInitOnlyInt = 1,
            RequiredInt = 2,
            InitOnlyInt = 3,

            requiredField = true,
        }, p);

        yield return Create(new StructRecordWithRequiredAndInitOnlyProperties(1, 2, 3)
        {
            RequiredAndInitOnlyString = "str1",
            RequiredString = "str2",
            InitOnlyString = "str3",

            RequiredAndInitOnlyInt = 1,
            RequiredInt = 2,
            InitOnlyInt = 3,

            requiredField = true,
        }, p);

        yield return Create(new ClassWithDefaultConstructorAndSingleRequiredProperty { Value = 42 }, p);
        yield return Create(new ClassWithParameterizedConstructorAnd2OptionalSetters(42), p);
        yield return Create(new ClassWithParameterizedConstructorAnd10OptionalSetters(42), p);
        yield return Create(new ClassWithParameterizedConstructorAnd70OptionalSetters(42), p);

        yield return Create(new ClassRecord(0, 1, 2, 3), p);
        yield return Create(new StructRecord(0, 1, 2, 3), p);
        yield return Create(new LargeClassRecord(), p);

        yield return Create(new ClassWithIndexer(), p);

        yield return Create(new RecordWithDefaultParams(), p);
        yield return Create(new RecordWithDefaultParams2(), p);

        yield return Create(new RecordWithNullableDefaultParams(), p);
        yield return Create(new RecordWithNullableDefaultParams2(), p);

        yield return Create(new RecordWithSpecialValueDefaultParams(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0), p);

        yield return Create(new RecordWithEnumAndNullableParams(MyEnum.A, MyEnum.C), p);
        yield return Create(new RecordWithNullableDefaultEnum(), p);

        yield return Create(new GenericContainer<string>.Inner { Value = "str" }, p);
        yield return Create(new GenericContainer<string>.Inner<string> { Value1 = "str", Value2 = "str2" }, p);

        yield return Create(new MyLinkedList<int>
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
        }, p);

        DateOnly today = DateOnly.Parse("2023-12-07");
        yield return Create(new Todos(
            [ new (Id: 0, "Wash the dishes.", today, Status.Done),
              new (Id: 1, "Dry the dishes.", today, Status.Done),
              new (Id: 2, "Turn the dishes over.", today, Status.InProgress),
              new (Id: 3, "Walk the kangaroo.", today.AddDays(1), Status.NotStarted),
              new (Id: 4, "Call Grandma.", today.AddDays(1), Status.NotStarted)]), p);

        yield return Create(new RecordWith21ConstructorParameters(
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2",
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2",
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2"), p);

        yield return Create(new RecordWith42ConstructorParameters(
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2",
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2",
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2",
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2",
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2",
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2"), p);

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
        }, p);

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
        }, p);

        yield return Create(new ClassWith40RequiredMembers
        {
            r00 = 00, r01 = 01, r02 = 02, r03 = 03, r04 = 04, r05 = 05, r06 = 06, r07 = 07, r08 = 08, r09 = 09, 
            r10 = 10, r11 = 11, r12 = 12, r13 = 13, r14 = 14, r15 = 15, r16 = 16, r17 = 17, r18 = 18, r19 = 19,
            r20 = 20, r21 = 21, r22 = 22, r23 = 23, r24 = 24, r25 = 25, r26 = 26, r27 = 27, r28 = 28, r29 = 29, 
            r30 = 30, r31 = 31, r32 = 32, r33 = 33, r34 = 34, r35 = 35, r36 = 36, r37 = 37, r38 = 38, r39 = 39,
        }, p);

        yield return Create(new StructWith40RequiredMembers
        {
            r00 = 00, r01 = 01, r02 = 02, r03 = 03, r04 = 04, r05 = 05, r06 = 06, r07 = 07, r08 = 08, r09 = 09, 
            r10 = 10, r11 = 11, r12 = 12, r13 = 13, r14 = 14, r15 = 15, r16 = 16, r17 = 17, r18 = 18, r19 = 19,
            r20 = 20, r21 = 21, r22 = 22, r23 = 23, r24 = 24, r25 = 25, r26 = 26, r27 = 27, r28 = 28, r29 = 29, 
            r30 = 30, r31 = 31, r32 = 32, r33 = 33, r34 = 34, r35 = 35, r36 = 36, r37 = 37, r38 = 38, r39 = 39,
        }, p);

        yield return Create(new StructWith40RequiredMembersAndDefaultCtor
        {
            r00 = 00, r01 = 01, r02 = 02, r03 = 03, r04 = 04, r05 = 05, r06 = 06, r07 = 07, r08 = 08, r09 = 09, 
            r10 = 10, r11 = 11, r12 = 12, r13 = 13, r14 = 14, r15 = 15, r16 = 16, r17 = 17, r18 = 18, r19 = 19,
            r20 = 20, r21 = 21, r22 = 22, r23 = 23, r24 = 24, r25 = 25, r26 = 26, r27 = 27, r28 = 28, r29 = 29, 
            r30 = 30, r31 = 31, r32 = 32, r33 = 33, r34 = 34, r35 = 35, r36 = 36, r37 = 37, r38 = 38, r39 = 39,
        }, p);

        yield return Create(new ClassWithInternalMembers { X = 1, Y = 2, Z = 3, W = 4, internalField = 5 }, p, doesNotRoundtrip: true);

        yield return CreateSelfProvided(new PersonClass("John", 40));
        yield return CreateSelfProvided(new PersonStruct("John", 40));
        yield return CreateSelfProvided<IPersonInterface>(new IPersonInterface.Impl("John", 40));
        yield return CreateSelfProvided<PersonAbstractClass>(new PersonAbstractClass.Impl("John", 40));
        yield return CreateSelfProvided(new PersonRecord("John", 40));
        yield return CreateSelfProvided(new PersonRecordStruct("John", 40));

        static TestCase<T, TProvider> Create<T, TProvider>(T value, TProvider provider, bool isStack = false, bool doesNotRoundtrip = false) 
            where TProvider : ITypeShapeProvider<T> 
            => new(value) { IsStack = isStack, DoesNotRoundtrip = doesNotRoundtrip };

        static TestCase<T, T> CreateSelfProvided<T>(T value) where T : ITypeShapeProvider<T>
            => new(value);
    }
}

public class DerivedList : List<int> { }
public class DerivedDictionary : Dictionary<string, string> { }

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

internal class MyLinkedList<T>
{
    public T? Value { get; set; }
    public MyLinkedList<T>? Next { get; set; }
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

public class ClassWithIndexer
{
    public string this[int i]
    {
        get => i.ToString();
        set { }
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

public class  ClassWithDefaultConstructorAndSingleRequiredProperty
{
    public required int Value { get; set; }
}

public class ClassWithParameterizedConstructorAnd2OptionalSetters(int x1)
{
    public int X1 { get; set; } = x1;
    public int X2 { get; set; }
}

public class ClassWithParameterizedConstructorAnd10OptionalSetters(int x01)
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

public class ClassWithParameterizedConstructorAnd70OptionalSetters(int x01)
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

public class ClassWithNullabilityAttributes
{
    private string? _maybeNull = "";
    private string? _allowNull = "";
    private string? _notNull = "";
    private string? _disallowNull = "";

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
        get => _allowNull ?? "";
        set => _allowNull = value;
    }

    [NotNull]
    public string? NotNullProperty
    {
        get => _notNull ?? "";
        set => _notNull = value;
    }

    [DisallowNull]
    public string? DisallowNullProperty
    {
        get => _disallowNull;
        set => _disallowNull = value;
    }

    [MaybeNull]
    public string MaybeNullField = "";
    [AllowNull]
    public string AllowNullField = "";
    [NotNull]
    public string? NotNullField = "";
    [DisallowNull]
    public string? DisallowNullField = "";
}

public class ClassWithStructNullabilityAttributes
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

public record ParameterlessRecord();
public record struct ParameterlessStructRecord();
public record SimpleRecord(int value);
public record NonNullStringRecord(string value);
public record NullableStringRecord(string? value);
public record GenericRecord<T>(T value);
public record NotNullGenericRecord<T>(T value) where T : notnull;
public record NotNullClassGenericRecord<T>(T value) where T : class;
public record NullClassGenericRecord<T>(T value) where T : class?;
#nullable disable
public record NullObliviousGenericRecord<T>(T value);
#nullable restore

public record ClassRecord(int x, int? y, int z, int w);
public record struct StructRecord(int x, int y, int z, int w);

public record RecordWithDefaultParams(bool x1 = true, byte x2 = 10, sbyte x3 = 10, char x4 = 'x', ushort x5 = 10, short x6 = 10, long x7 = 10);
public record RecordWithDefaultParams2(ulong x1 = 10, float x2 = 3.1f, double x3 = 3.1d, decimal x4 = -3.1415926m, string x5 = "str", string? x6 = null, object? x7 = null);

public record RecordWithNullableDefaultParams(bool? x1 = true, byte? x2 = 10, sbyte? x3 = 10, char? x4 = 'x', ushort? x5 = 10, short? x6 = 10, long? x7 = 10);
public record RecordWithNullableDefaultParams2(ulong? x1 = 10, float? x2 = 3.1f, double? x3 = 3.1d, decimal? x4 = -3.1415926m, string? x5 = "str", string? x6 = null, object? x7 = null);

public record RecordWithSpecialValueDefaultParams(
    double d1 = double.PositiveInfinity, double d2 = double.NegativeInfinity, double d3 = double.NaN,
    double? dn1 = double.PositiveInfinity, double? dn2 = double.NegativeInfinity, double? dn3 = double.NaN,
    float f1 = float.PositiveInfinity, float f2 = float.NegativeInfinity, float f3 = float.NaN,
    float? fn1 = float.PositiveInfinity, float? fn2 = float.NegativeInfinity, float? fn3 = float.NaN,
    string s = "\"😀葛🀄🤯𐐀𐐨\"", char c = '\'');

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

public class ClassWithInternalMembers
{
    public int X { get; set; }
    internal int Y { get; set; }
    public int Z { internal get; set; }
    public int W { get; internal set; }

    internal int internalField;
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

public record Todos(Todo[] Items);

public record Todo(int Id, string? Title, DateOnly? DueBy, Status Status);

public enum Status { NotStarted, InProgress, Done }

[GenerateShape<object>]
[GenerateShape<bool>]
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
[GenerateShape<Uri>]
[GenerateShape<Version>]
[GenerateShape<byte[]>]
[GenerateShape<int[]>]
[GenerateShape<int[][]>]
[GenerateShape<int[,]>]
[GenerateShape<int[,,]>]
[GenerateShape<Memory<int>>]
[GenerateShape<ReadOnlyMemory<int>>]
[GenerateShape<List<string>>]
[GenerateShape<List<byte>>]
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
[GenerateShape<DerivedList>]
[GenerateShape<DerivedDictionary>]
[GenerateShape<StructList<int>>]
[GenerateShape<StructDictionary<string, string>>]
[GenerateShape<PocoWithListAndDictionaryProps>]
[GenerateShape<BaseClass>]
[GenerateShape<DerivedClass>]
[GenerateShape<IBaseInterface>]
[GenerateShape<IDerivedInterface>]
[GenerateShape<IDerived2Interface>]
[GenerateShape<IDerived3Interface>]
[GenerateShape<IDiamondInterface>]
[GenerateShape<ParameterlessRecord>]
[GenerateShape<ParameterlessStructRecord>]
[GenerateShape<SimpleRecord>]
[GenerateShape<NonNullStringRecord>]
[GenerateShape<NullableStringRecord>]
[GenerateShape<GenericRecord<int>>]
[GenerateShape<GenericRecord<string>>]
[GenerateShape<GenericRecord<GenericRecord<bool>>>]
[GenerateShape<GenericRecord<GenericRecord<int>>>]
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
[GenerateShape<ComplexStruct>]
[GenerateShape<ComplexStructWithProperties>]
[GenerateShape<StructWithDefaultCtor>]
[GenerateShape<ClassWithReadOnlyField>]
[GenerateShape<ClassWithRequiredField>]
[GenerateShape<StructWithRequiredField>]
[GenerateShape<ClassWithRequiredProperty>]
[GenerateShape<StructWithRequiredProperty>]
[GenerateShape<ClassWithRequiredAndInitOnlyProperties>]
[GenerateShape<ClassWithIndexer>]
[GenerateShape<StructWithRequiredAndInitOnlyProperties>]
[GenerateShape<ClassRecordWithRequiredAndInitOnlyProperties>]
[GenerateShape<StructRecordWithRequiredAndInitOnlyProperties>]
[GenerateShape<ClassWithDefaultConstructorAndSingleRequiredProperty>]
[GenerateShape<ClassWithParameterizedConstructorAnd2OptionalSetters>]
[GenerateShape<ClassWithParameterizedConstructorAnd10OptionalSetters>]
[GenerateShape<ClassWithParameterizedConstructorAnd70OptionalSetters>]
[GenerateShape<StructWithRequiredPropertyAndDefaultCtor>]
[GenerateShape<StructWithRequiredFieldAndDefaultCtor>]
[GenerateShape<ClassWithSetsRequiredMembersCtor>]
[GenerateShape<StructWithSetsRequiredMembersCtor>]
[GenerateShape<ClassRecord>]
[GenerateShape<StructRecord>]
[GenerateShape<LargeClassRecord>]
[GenerateShape<RecordWithDefaultParams>]
[GenerateShape<RecordWithDefaultParams2>]
[GenerateShape<RecordWithNullableDefaultParams>]
[GenerateShape<RecordWithNullableDefaultParams2>]
[GenerateShape<RecordWithSpecialValueDefaultParams>]
[GenerateShape<RecordWithEnumAndNullableParams>]
[GenerateShape<ClassWithNullabilityAttributes>]
[GenerateShape<ClassWithStructNullabilityAttributes>]
[GenerateShape<NotNullGenericRecord<string>>]
[GenerateShape<NotNullClassGenericRecord<string>>]
[GenerateShape<NullClassGenericRecord<string>>]
[GenerateShape<NullObliviousGenericRecord<string>>]
[GenerateShape<MyLinkedList<int>>]
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
[GenerateShape<RecordWith21ConstructorParameters>]
[GenerateShape<RecordWith42ConstructorParameters>]
[GenerateShape<RecordWith42ConstructorParametersAndRequiredProperties>]
[GenerateShape<StructRecordWith42ConstructorParametersAndRequiredProperties>]
[GenerateShape<ClassWith40RequiredMembers>]
[GenerateShape<StructWith40RequiredMembers>]
[GenerateShape<StructWith40RequiredMembersAndDefaultCtor>]
[GenerateShape<RecordWithNullableDefaultEnum>]
[GenerateShape<BindingModel>]
[GenerateShape<List<BindingModel>>]
[GenerateShape<GenericRecord<BindingModel>>]
[GenerateShape<Dictionary<string, BindingModel>>]
[GenerateShape<PersonClass>]
[GenerateShape<PersonStruct>]
[GenerateShape<PersonAbstractClass>]
[GenerateShape<IPersonInterface>]
[GenerateShape<PersonRecord>]
[GenerateShape<PersonRecordStruct>]
[GenerateShape<CollectionWithBuilderAttribute>]
[GenerateShape<GenericCollectionWithBuilderAttribute<int>>]
[GenerateShape<ClassWithInternalMembers>]
[GenerateShape<Todos>]
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