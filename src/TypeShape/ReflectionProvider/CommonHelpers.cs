using System.Diagnostics;

namespace TypeShape;

internal static class CommonHelpers
{
    /// <summary>
    /// Traverses a DAG and returns its nodes applying topological sorting to the result.
    /// </summary>
    public static T[] TraverseGraphWithTopologicalSort<T>(T entryNode, Func<T, IList<T>> getChildren, IEqualityComparer<T>? comparer = null)
        where T : notnull
    {
        comparer ??= EqualityComparer<T>.Default;

        // Uses a standard implementation of Kahn's algorithm.
        
        // Step 1. Traverse and build a model for the DAG, representing each node with an integer.

        var nodeIndex = new Dictionary<T, int>(comparer) { [entryNode] = 0 }; // mapping nodes to integers.
        var values = new List<T> { entryNode }; // inverse mapping from integers to nodes.
        var graph = new List<List<int>?>(); // mapping nodes to a child nodes.
        var queue = new Queue<int>(); // the set of nodes that don't have any child nodes.

        for (int i = 0; i < values.Count; i++)
        {
            T next = values[i];
            List<int>? children = null;

            foreach (T child in getChildren(next))
            {
                if (!nodeIndex.TryGetValue(child, out int index))
                {
                    // this is the first time we're visiting this child.
                    // Assign it an index and add it to the maps.

                    index = values.Count;
                    nodeIndex.Add(child, index);
                    values.Add(child);
                }

                (children ??= new()).Add(index);
            }

            graph.Add(children);

            if (children is null)
            {
                queue.Enqueue(i);
            }
        }

        Debug.Assert(queue.Count > 0, "Does not define a DAG.");

        // Step 2. Build the sorted array, walking from the children without nodes downward.
        var sorted = new T[values.Count];
        int idx = sorted.Length;

        do
        {
            int nextIndex = queue.Dequeue();
            sorted[--idx] = values[nextIndex];

            for (int i = 0; i < graph.Count; i++)
            {
                if (graph[i] is { } children && children.Remove(nextIndex) && children.Count == 0)
                {
                    queue.Enqueue(i);
                }
            }

        } while (queue.Count > 0);

        Debug.Assert(idx == 0);
        return sorted;
    }
}
