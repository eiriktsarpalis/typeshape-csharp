namespace TypeShape.Examples;

public interface ISpanEqualityComparer<T>
{
    int GetHashCode(ReadOnlySpan<T> buffer);
    bool Equals(ReadOnlySpan<T> x, ReadOnlySpan<T> y);
}

public static class ByteSpanEqualityComparer
{
    public static ISpanEqualityComparer<byte> Ordinal { get; } = new OrdinalEqualityComparer();

    private sealed class OrdinalEqualityComparer : ISpanEqualityComparer<byte>
    {
        public bool Equals(ReadOnlySpan<byte> x, ReadOnlySpan<byte> y)
            => x.SequenceEqual(y);

        public int GetHashCode(ReadOnlySpan<byte> buffer)
        {
            var hc = new HashCode();
            hc.AddBytes(buffer);
            return hc.ToHashCode();
        }
    }
}

public static class CharSpanEqualityComparer
{
    public static ISpanEqualityComparer<char> Ordinal { get; } = new StringComparisonEqualityComparer(StringComparison.Ordinal);
    public static ISpanEqualityComparer<char> OrdinalIgnoreCase { get; } = new StringComparisonEqualityComparer(StringComparison.OrdinalIgnoreCase);

    private sealed class StringComparisonEqualityComparer(StringComparison comparison) : ISpanEqualityComparer<char>
    {
        public int GetHashCode(ReadOnlySpan<char> buffer)
            => string.GetHashCode(buffer, comparison);

        public bool Equals(ReadOnlySpan<char> x, ReadOnlySpan<char> y)
            => x.Equals(y, comparison);
    }
}