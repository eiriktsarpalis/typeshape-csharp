using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace PolyType.Tests;

[GenerateShape<Half>]
[GenerateShape<BigInteger>]
[GenerateShape<Int128>]
[GenerateShape<MyEnum>]
[GenerateShape<int>]
[GenerateShape<int?>]
[GenerateShape<long>]
[GenerateShape<bool>]
[GenerateShape<object>]
[GenerateShape<string>]
[GenerateShape<string[]>]
[GenerateShape<char>]
[GenerateShape<byte[]>]
[GenerateShape<float>]
[GenerateShape<double>]
[GenerateShape<decimal>]
[GenerateShape<byte>]
[GenerateShape<DateOnly>]
[GenerateShape<TimeOnly>]
[GenerateShape<DateTime>]
[GenerateShape<DateTimeOffset>]
[GenerateShape<TimeSpan>]
[GenerateShape<Guid>]
[GenerateShape<int[]>]
[GenerateShape<int[][]>]
[GenerateShape<(int, string)>]
[GenerateShape<Dictionary<string, int>>]
[GenerateShape<Dictionary<string, string>>]
[GenerateShape<ImmutableDictionary<string, string>>]
[GenerateShape<ImmutableSortedDictionary<string, string>>]
[GenerateShape<MyLinkedList<int>>]
[GenerateShape<MyLinkedList<SimpleRecord>>]
[GenerateShape<ImmutableArray<int>>]
[GenerateShape<ImmutableQueue<int>>]
[GenerateShape<List<int>>]
[GenerateShape<ImmutableList<string>>]
internal partial class SourceGenProvider;

internal class MyLinkedList<T>
{
    public T? Value { get; set; }
    public MyLinkedList<T>? Next { get; set; }
}
