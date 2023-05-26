using System.Diagnostics;

namespace TypeShape;

internal static class CommonHelpers
{
    /// <summary>
    /// Traverses a DAG and returns its nodes applying topological sorting to the result.
    /// </summary>
    public static T[] TraverseGraphWithTopologicalSort<T>(T entryNode, Func<T, ICollection<T>> getChildren, IEqualityComparer<T>? comparer = null)
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
            ICollection<T> children = getChildren(next);
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

        } while (childlessQueue.Count > 0);

        Debug.Assert(idx == 0, "should have populated the entire sortedNodes array.");
        return sortedNodes;
    }
}
