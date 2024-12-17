namespace PolyType.Roslyn.Tests;

using Xunit;
using System.Collections;

public static class ImmutableEquatableCollectionTests
{
    [Fact]
    public static void ImmutableEquatableArray_EqualsSequenceEqualValue()
    {
        ImmutableEquatableArray<int> array1 = [1, 2, 3, 4, 5];
        ImmutableEquatableArray<int> array2 = [1, 2, 3, 4, 5];
        Assert.NotSame(array1, array2);
        Assert.Equal(array1.GetHashCode(), array2.GetHashCode());
        Assert.Equal(array1, array2);
        Assert.True(array1.Equals((object)array2));
    }
    
    [Fact]
    public static void ImmutableEquatableArray_DoesNotEqualNonEqualValues()
    {
        ImmutableEquatableArray<int> array1 = [1, 2, 3, 4, 5];
        ImmutableEquatableArray<int> array2 = [1, 2, 3, 4, 7];
        Assert.NotEqual(array1, array2);
        Assert.False(array1.Equals((object)array2));
    }
    
    [Fact]
    public static void ImmutableEquatableArray_IReadOnlyListOfTView()
    {
        ImmutableEquatableArray<int> array1 = [1, 2, 3, 4, 5];
        IReadOnlyList<int> list = array1;

        Assert.Equal([1, 2, 3, 4, 5], list);
        Assert.Equal(1, list[0]);
        Assert.Equal(5, list.Count);
    }
    
    [Fact]
    public static void ImmutableEquatableArray_IListOfTView()
    {
        ImmutableEquatableArray<int> array1 = [1, 2, 3, 4, 5];
        IList<int> list = array1;

        Assert.Equal([1, 2, 3, 4, 5], list);
        Assert.Equal(1, list[0]);
        Assert.Equal(5, list.Count);
        Assert.True(list.IsReadOnly);
        Assert.True(list.Contains(1));
        Assert.Equal(3, list.IndexOf(4));
        
        int[] buffer = new int[5];
        list.CopyTo(buffer, 0);
        Assert.Equal(list, buffer);
        
        Assert.Throws<InvalidOperationException>(() => list.RemoveAt(2));
        Assert.Throws<InvalidOperationException>(() => list.Remove(1));
        Assert.Throws<InvalidOperationException>(() => list.Insert(0, 5));
        Assert.Throws<InvalidOperationException>(() => list.Add(5));
        Assert.Throws<InvalidOperationException>(() => list.Clear());
        Assert.Throws<InvalidOperationException>(() => list[0] = -1);
    }

    [Fact]
    public static void ImmutableEquatableArray_IListView()
    {
        ImmutableEquatableArray<int> array1 = [1, 2, 3, 4, 5];
        IList list = array1;
        
        Assert.True(list.IsReadOnly);
        Assert.True(list.IsFixedSize);
        Assert.Equal(1, list[0]);
        Assert.Equal(5, list.Count);
        Assert.Equal(3, list.IndexOf(4));
        Assert.True(list.Contains(1));
        Assert.False(list.IsSynchronized);
        Assert.Same(list, list.SyncRoot);
        
        int[] buffer = new int[5];
        list.CopyTo(buffer, 0);
        Assert.Equal(list, buffer);
        
        Assert.Throws<InvalidOperationException>(() => list.RemoveAt(2));
        Assert.Throws<InvalidOperationException>(() => list.Remove(1));
        Assert.Throws<InvalidOperationException>(() => list.Insert(0, 5));
        Assert.Throws<InvalidOperationException>(() => list.Add(5));
        Assert.Throws<InvalidOperationException>(() => list.Clear());
        Assert.Throws<InvalidOperationException>(() => list[0] = -1);
    }

    [Fact]
    public static void ImmutableEquatableSet_EqualsSetEqualValue()
    {
        ImmutableEquatableSet<int> set1 = [1, 2, 3, 4, 5];
        ImmutableEquatableSet<int> set2 = [1, 2, 3, 5, 4];
        
        Assert.NotSame(set1, set2);
        Assert.Equal(set1.GetHashCode(), set2.GetHashCode());
        Assert.Equal(set1, set2);
        Assert.True(set1.Equals((object)set2));
    }

    [Fact]
    public static void ImmutableEquatableSet_DoesNotEqualNonEqualValues()
    {
        ImmutableEquatableSet<int> set1 = [1, 2, 3, 4, 5];
        ImmutableEquatableSet<int> set2 = [1, 2, 3, 4, 7];
        
        Assert.NotEqual(set1, set2);
        Assert.False(set1.Equals((object)set2));
    }

    [Fact]
    public static void ImmutableEquatableSet_ISetOfTView()
    {
        ImmutableEquatableSet<int> set1 = [1, 2, 3, 4, 5];
        ISet<int> set2 = set1;

        Assert.Equal([1, 2, 3, 4, 5], set2);
        Assert.Equal(5, set2.Count);
        Assert.True(set2.Contains(1));
        Assert.True(set2.IsSubsetOf(set2));
        Assert.True(set2.IsSupersetOf(set2));
        Assert.False(set2.IsProperSubsetOf(set2));
        Assert.False(set2.IsProperSupersetOf(set2));
        Assert.True(set2.Overlaps(set2));
        Assert.True(set2.SetEquals(set2));
        Assert.True(set2.IsReadOnly);
        
        int[] buffer = new int[5];
        set2.CopyTo(buffer, 0);
        Assert.Equal(set2, buffer);
        
        Assert.Throws<InvalidOperationException>(() => ((ICollection<int>)set2).Add(5));
        Assert.Throws<InvalidOperationException>(() => set2.Add(5));
        Assert.Throws<InvalidOperationException>(() => set2.Remove(2));
        Assert.Throws<InvalidOperationException>(() => set2.Clear());
        Assert.Throws<InvalidOperationException>(() => set2.UnionWith(set2));
        Assert.Throws<InvalidOperationException>(() => set2.ExceptWith(set2));
        Assert.Throws<InvalidOperationException>(() => set2.IntersectWith(set2));
        Assert.Throws<InvalidOperationException>(() => set2.SymmetricExceptWith(set2));
    }
    
    [Fact]
    public static void ImmutableEquatableSet_IReadOnlyCollectionOfTView()
    {
        ImmutableEquatableSet<int> set1 = [1, 2, 3, 4, 5];
        IReadOnlyCollection<int> set2 = set1;

        Assert.Equal([1, 2, 3, 4, 5], set2);
        Assert.Equal(5, set2.Count);
        Assert.Contains(1, set2);
    }
    
    [Fact]
    public static void ImmutableEquatableSet_ICollectionView()
    {
        ImmutableEquatableSet<int> set1 = [1, 2, 3, 4, 5];
        ICollection set2 = set1;
        
        Assert.Equal(5, set2.Count);
        Assert.False(set2.IsSynchronized);
        Assert.Same(set2, set2.SyncRoot);
        
        int[] buffer = new int[5];
        Assert.Throws<InvalidOperationException>(() => set2.CopyTo(buffer, 0));
    }
    
    [Fact]
    public static void ImmutableEquatableDictionary_EqualsSequenceEqualValue()
    {
        ImmutableEquatableDictionary<int, int> array1 = Enumerable.Range(1, 10).ToImmutableEquatableDictionary(i => i, i => i);
        ImmutableEquatableDictionary<int, int> array2 = Enumerable.Range(1, 10).ToImmutableEquatableDictionary(i => i, i => i);
        
        Assert.NotSame(array1, array2);
        Assert.Equal(array1.GetHashCode(), array2.GetHashCode());
        Assert.Equal(array1, array2);
        Assert.True(array1.Equals((object)array2));
    }

    [Fact]
    public static void ImmutableEquatableDictionary_DoesNotEqualNonEqualValues()
    {
        ImmutableEquatableDictionary<int, int> dictionary1 = Enumerable.Range(1, 10).ToImmutableEquatableDictionary(i => i, i => i);
        ImmutableEquatableDictionary<int, int> dictionary2 = Enumerable.Range(1, 10).ToImmutableEquatableDictionary(i => i, i => i == 5 ? 20 : i);
        Assert.NotEqual(dictionary1, dictionary2);
        Assert.False(dictionary1.Equals((object)dictionary2));
    }

    [Fact]
    public static void ImmutableEquatableDictionary_IReadOnlyDictionaryOfTView()
    {
        ImmutableEquatableDictionary<int, int> dictionary1 = Enumerable.Range(1, 10).ToImmutableEquatableDictionary(i => i, i => i);
        IReadOnlyDictionary<int, int> dictionary2 = dictionary1;
        
        Assert.Equal(dictionary1, dictionary2);
        Assert.Equal(10, dictionary2.Count);
        Assert.Equal(dictionary1.Keys, dictionary2.Keys);
        Assert.Equal(dictionary1.Values, dictionary2.Values);
        Assert.True(dictionary2.ContainsKey(1));
        Assert.True(dictionary2.TryGetValue(1, out int _));
    }
    
    [Fact]
    public static void ImmutableEquatableDictionary_IDictionaryOfTView()
    {
        ImmutableEquatableDictionary<int, int> dictionary1 = Enumerable.Range(1, 10).ToImmutableEquatableDictionary(i => i, i => i);
        IDictionary<int, int> dictionary2 = dictionary1;
        
        Assert.Equal(dictionary1, dictionary2);
        Assert.Equal(10, dictionary2.Count);
        Assert.Equal(dictionary1.Keys, dictionary2.Keys);
        Assert.Equal(dictionary1.Values, dictionary2.Values);
        Assert.True(dictionary2.ContainsKey(1));
        Assert.True(dictionary2.Contains(new KeyValuePair<int, int>(1, 1)));
        Assert.True(dictionary2.TryGetValue(1, out int _));
        Assert.Equal(4, dictionary2[4]);
        Assert.True(dictionary2.IsReadOnly);
        
        KeyValuePair<int, int>[] buffer = new KeyValuePair<int, int>[10];
        dictionary2.CopyTo(buffer, 0);
        Assert.Equal(dictionary2, buffer);
        
        Assert.Throws<InvalidOperationException>(() => dictionary2.Remove(1));
        Assert.Throws<InvalidOperationException>(() => dictionary2.Clear());
        Assert.Throws<InvalidOperationException>(() => dictionary2.Add(55, 5));
        Assert.Throws<InvalidOperationException>(() => dictionary2[1] = 42);
        
        Assert.Throws<InvalidOperationException>(() => dictionary2[2] = 42);
        Assert.Throws<InvalidOperationException>(() => dictionary2.Add(new KeyValuePair<int, int>(1, 42)));
        Assert.Throws<InvalidOperationException>(() => dictionary2.Remove(new KeyValuePair<int, int>(1, 42)));
    }
    
    [Fact]
    public static void ImmutableEquatableDictionary_IDictionaryView()
    {
        ImmutableEquatableDictionary<int, int> dictionary1 = Enumerable.Range(1, 10).ToImmutableEquatableDictionary(i => i, i => i);
        IDictionary dictionary2 = dictionary1;
        
        Assert.Equal(dictionary1, dictionary2);
        Assert.Equal(10, dictionary2.Count);
        Assert.Equal(dictionary1.Keys, dictionary2.Keys);
        Assert.Equal(dictionary1.Values, dictionary2.Values);
        Assert.True(dictionary2.Contains(1));
        Assert.Equal(4, dictionary2[4]);
        Assert.True(dictionary2.IsReadOnly);
        Assert.True(dictionary2.IsFixedSize);
        Assert.False(dictionary2.IsSynchronized);
        Assert.Same(dictionary2, dictionary2.SyncRoot);
        
        KeyValuePair<int, int>[] buffer = new KeyValuePair<int, int>[10];
        dictionary2.CopyTo(buffer, 0);
        Assert.Equal(dictionary1, buffer);
        
        Assert.Throws<InvalidOperationException>(() => dictionary2.Remove(1));
        Assert.Throws<InvalidOperationException>(() => dictionary2.Clear());
        Assert.Throws<InvalidOperationException>(() => dictionary2.Add(55, 5));
        Assert.Throws<InvalidOperationException>(() => dictionary2[1] = 42);
        
        Assert.Throws<InvalidOperationException>(() => dictionary2[2] = 42);
        Assert.Throws<InvalidOperationException>(() => dictionary2.Remove(new KeyValuePair<int, int>(1, 42)));
    }

    [Fact]
    public static void ImmutableEquatableDictionary_Empty()
    {
        Assert.Same(ImmutableEquatableDictionary.Empty<int, int>(), ImmutableEquatableDictionary.Empty<int, int>());
        var empty = ImmutableEquatableDictionary.Empty<int, int>();
        Assert.Empty(empty);
    }
    
    [Fact]
    public static void ImmutableEquatableDictionary_KeyProjection()
    {
        var dict = Enumerable.Range(1, 10).ToImmutableEquatableDictionary(i => i);
        var dict2 = Enumerable.Range(1, 10).ToDictionary(i => i);
        Assert.Equal(dict, dict2);
    }
    
    [Fact]
    public static void ImmutableEquatableDictionary_FromKeyValuePairs()
    {
        var dict = Enumerable.Range(1, 10)
            .Select(i => new KeyValuePair<int, int>(i, i))
            .ToImmutableEquatableDictionary();
        var dict2 = Enumerable.Range(1, 10).ToDictionary(i => i);
        Assert.Equal(dict, dict2);
    }
}