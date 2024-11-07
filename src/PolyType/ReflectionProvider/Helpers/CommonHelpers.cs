using System.Diagnostics;

namespace PolyType;

internal static class CommonHelpers
{
    /// <summary>
    /// Traverses a DAG and returns its nodes applying topological sorting to the result.
    /// </summary>
    /// <typeparam name="T">Element type of the graph.</typeparam>
    /// <param name="entryNode">The entrypoint for the graph.</param>
    /// <param name="getChildren">The function getting the neighbours of the provided node.</param>
    /// <param name="comparer">The comparer determining equality.</param>
    /// <returns>An array of topologically ordered nodes.</returns>
    public static T[] TraverseGraphWithTopologicalSort<T>(T entryNode, Func<T, IReadOnlyCollection<T>> getChildren, IEqualityComparer<T>? comparer = null)
        where T : notnull
    {
        comparer ??= EqualityComparer<T>.Default;

        // Implements Kahn's algorithm.
        // Step 1. Traverse and build the graph, labeling each node with an integer.

        var nodes = new List<T> { entryNode }; // the integer-to-node mapping
        var nodeIndex = new Dictionary<T, int>(comparer) { [entryNode] = 0 }; // the node-to-integer mapping
        var adjacency = new List<bool[]?>(); // the growable adjacency matrix
        var childlessQueue = new Queue<int>(); // the queue of nodes without children or whose children have been visited

        for (int i = 0; i < nodes.Count; i++)
        {
            T next = nodes[i];
            IReadOnlyCollection<T> children = getChildren(next);
            int count = children.Count;

            if (count == 0)
            {
                adjacency.Add(null); // can use null in this row of the adjacency matrix.
                childlessQueue.Enqueue(i);
                continue;
            }

            var adjacencyRow = new bool[Math.Max(nodes.Count, count)];
            foreach (T childNode in children)
            {
                if (!nodeIndex.TryGetValue(childNode, out int index))
                {
                    // this is the first time we're encountering this node.
                    // Assign it an index and append it to the maps.

                    index = nodes.Count;
                    nodeIndex.Add(childNode, index);
                    nodes.Add(childNode);
                }

                // Grow the adjacency row as appropriate.
                if (index >= adjacencyRow.Length)
                {
                    Array.Resize(ref adjacencyRow, index + 1);
                }

                // Set the relevant bit in the adjacency row.
                adjacencyRow[index] = true;
            }

            // Append the row to the adjacency matrix.
            adjacency.Add(adjacencyRow);
        }

        Debug.Assert(childlessQueue.Count > 0, "The graph contains cycles.");

        // Step 2. Build the sorted array, walking from the nodes without children upward.
        var sortedNodes = new T[nodes.Count];
        int idx = sortedNodes.Length;

        do
        {
            int nextIndex = childlessQueue.Dequeue();
            sortedNodes[--idx] = nodes[nextIndex];

            // Iterate over the adjacency matrix, removing any occurrence of nextIndex.
            for (int i = 0; i < adjacency.Count; i++)
            {
                if (adjacency[i] is { } childMap && i < childMap.Length && childMap[nextIndex])
                {
                    childMap[nextIndex] = false;

                    if (childMap.AsSpan().IndexOf(true) == -1)
                    {
                        // nextIndex was the last child removed from i, add to queue.
                        childlessQueue.Enqueue(i);
                    }
                }
            }
        }
        while (childlessQueue.Count > 0);

        Debug.Assert(idx == 0, "should have populated the entire sortedNodes array.");
        return sortedNodes;
    }

    public static int CombineHashCodes(int h1, int h2)
    {
        // RyuJIT optimizes this to use the ROL instruction
        // Related GitHub pull request: https://github.com/dotnet/coreclr/pull/1830
        uint rol5 = ((uint)h1 << 5) | ((uint)h1 >> 27);
        return ((int)rol5 + h1) ^ h2;
    }

    /// <summary>
    /// A string comparer that equates "SomeIdentifier" with "someIdentifier"
    /// </summary>
    public sealed class CamelCaseInvariantComparer : EqualityComparer<string>
    {
        public static CamelCaseInvariantComparer Instance { get; } = new();

        public override bool Equals(string? left, string? right)
        {
            if (left is null || right is null)
            {
                return left == right;
            }

            if (left.Length != right.Length)
            {
                return false;
            }

            if (left.Length == 0)
            {
                return true;
            }

            // NB this ignores surrogate pairs that are letters
            return char.ToLowerInvariant(left[0]) == char.ToLowerInvariant(right[0]) &&
                   left.AsSpan(start: 1).SequenceEqual(right.AsSpan(start: 1));
        }

        public override int GetHashCode(string text)
        {
            return text is [] ? 0 :
                CombineHashCodes(
                    char.ToLowerInvariant(text[0]).GetHashCode(),
                    GetOrdinalHashCode(text.AsSpan(start: 1)));

            static int GetOrdinalHashCode(ReadOnlySpan<char> span)
            {
#if NETCOREAPP
                return string.GetHashCode(span, StringComparison.Ordinal);
#else
                const int prime = 31;
                int hash = 17;

                for (int i = 0; i < span.Length; i++)
                {
                    hash = unchecked((hash * prime) + span[i]);
                }

                return hash;
#endif
            }
        }
    }

    public static EqualityComparer<(T1, T2)> CreateTupleComparer<T1, T2>(IEqualityComparer<T1> left, IEqualityComparer<T2> right)
        => new TupleComparer<T1, T2>(left, right);

    private sealed class TupleComparer<T1, T2>(IEqualityComparer<T1> left, IEqualityComparer<T2> right) : EqualityComparer<(T1, T2)>
    {
        public override bool Equals((T1, T2) x, (T1, T2) y)
            => left.Equals(x.Item1, y.Item1) && right.Equals(y.Item2, y.Item2);

        public override int GetHashCode((T1, T2) obj)
            => CombineHashCodes(
                obj.Item1 is { } item1 ? left.GetHashCode(item1) : 0,
                obj.Item2 is { } item2 ? right.GetHashCode(item2) : 0);
    }
}