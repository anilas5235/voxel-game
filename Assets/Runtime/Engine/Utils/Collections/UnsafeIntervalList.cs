using System;
using System.Text;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Runtime.Engine.Utils.Collections
{
    /// <summary>
    /// Didn't see much difference in SOA or AOS performance wise, performance depends on the way elements
    /// would be accessed in our case AOS might be better, should profile and see
    /// </summary>
    [BurstCompile]
    public struct UnsafeIntervalList
    {
        // Array of structs impl
        private struct Node
        {
            public ushort ID;
            public int Count;

            public Node(ushort id, int count)
            {
                ID = id;
                Count = count;
            }
        }

        private UnsafeList<Node> _internal;

        // Struct of arrays impl
        // private UnsafeList<int> _Ids;
        // private UnsafeList<int> _Counts;

        public int Length;

        public int CompressedLength => _internal.Length;

        public UnsafeIntervalList(int capacity, Allocator allocator)
        {
            _internal = new UnsafeList<Node>(capacity, allocator);
            Length = 0;
        }

        public void Dispose()
        {
            _internal.Dispose();
        }

        public int NodeIndex(int index) => BinarySearch(index);

        public void AddInterval(ushort id, int count)
        {
            Length += count;
            _internal.Add(new Node(id, Length));
        }

        /// <summary>
        /// COMPLEXITY : O(Log(n))
        /// </summary>
        /// <param name="index">Index at which value to fetch</param>
        /// <returns>Value at index</returns>
        /// <exception cref="IndexOutOfRangeException">Debug only</exception>
        public ushort Get(int index)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (index >= Length)
                throw new IndexOutOfRangeException($"{index} is out of range for the given data of length {Length}");
#endif
            return _internal[BinarySearch(index)].ID;
        }

        /// <summary>
        /// Not sure if I got all the cases
        /// COMPLEXITY : O(Log(n)), could be O(n) also depending on RemoveRange and InsertRange
        /// REF: https://github.com/mikolalysenko/NodeMinecraftThing/blob/master/client/voxels.js
        /// </summary>
        /// <param name="index">index to set at</param>
        /// <param name="id">value to set at</param>
        /// <exception cref="IndexOutOfRangeException">Debug only</exception>
        public bool Set(int index, ushort id)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (index >= Length)
                throw new IndexOutOfRangeException($"{index} is out of range for the given data of length {Length}");
#endif

            int nodeIndex = BinarySearch(index);

            int block = _internal[nodeIndex].ID;

            if (block == id) return false; // No Change

            (int leftItem, int leftNodeIndex) = LeftOf(index, nodeIndex);
            (int rightItem, int rightNodeIndex) = RightOf(index, nodeIndex);

            // Nodes are returned by value, so we need to update them back in the array

            if (id == leftItem && id == rightItem)
            {
                // [X,A,X] -> [X,X,X]
                Node leftNode = _internal[leftNodeIndex];

                leftNode.Count = _internal[rightNodeIndex].Count;

                _internal[leftNodeIndex] = leftNode;

                _internal.RemoveRange(nodeIndex, 2);
            }
            else if (id == leftItem)
            {
                // [X,A,A,Y] -> [X,X,A,Y]
                Node leftNode = _internal[leftNodeIndex]; // This is returned by value
                Node node = _internal[nodeIndex];

                leftNode.Count++;

                _internal[leftNodeIndex] = leftNode;

                if (leftNode.Count == node.Count) _internal.RemoveRange(nodeIndex, 1); // [X,A,Y] -> [X,X,Y]
            }
            else if (id == rightItem)
            {
                // [X,A,A,Y] -> [X,A,Y,Y]
                Node leftNode = _internal[leftNodeIndex];
                Node node = _internal[nodeIndex];

                node.Count--;

                _internal[nodeIndex] = node;

                if (leftNode.Count == node.Count) _internal.RemoveRange(nodeIndex, 1); // [X,A,Y] -> [X,Y,Y]
            }
            else
            {
                // No Coalesce
                if (block == leftItem && block == rightItem)
                {
                    // [X,X,X] -> [X,A,X]
                    // Unity docs says that InsertRange creates duplicates of node at node_index but in the
                    // debugger I have seen junk values sometimes, so to be safe we set the values of
                    // each newly created node to the correct value.
                    _internal.InsertRange(nodeIndex, 2);

                    Node leftNode = _internal[nodeIndex];
                    Node node = _internal[nodeIndex + 1];
                    Node rightNode = _internal[nodeIndex + 2];

                    leftNode.Count = index;

                    node.ID = id;
                    node.Count = index + 1;

                    rightNode.ID = leftNode.ID;

                    _internal[nodeIndex] = leftNode;
                    _internal[nodeIndex + 1] = node;
                    _internal[nodeIndex + 2] = rightNode;
                }
                else if (block != leftItem && block == rightItem)
                {
                    // [X,Y,Y] -> [X,A,Y]
                    _internal.InsertRange(nodeIndex, 1);

                    Node node = _internal[nodeIndex];

                    node.ID = id;
                    node.Count = _internal[leftNodeIndex].Count + 1;

                    _internal[nodeIndex] = node;
                }
                else if (block == leftItem && block != rightItem)
                {
                    // [X,X,Y] -> [X,A,Y]
                    _internal.InsertRange(nodeIndex, 1);

                    Node node = _internal[nodeIndex + 1];
                    Node leftNode = _internal[leftNodeIndex];

                    node.ID = id;
                    node.Count = leftNode.Count;

                    leftNode.Count--;

                    _internal[nodeIndex + 1] = node;
                    _internal[leftNodeIndex] = leftNode;
                }
                else
                {
                    // [X,Y,X] -> [X,A,X] or [X,Y,Z] -> [X,A,Z]
                    Node node = _internal[nodeIndex];

                    node.ID = id;

                    _internal[nodeIndex] = node;
                }
            }

            return true;
        }

        public int LeftOf(int index)
        {
            return LeftOf(index, NodeIndex(index)).Item1;
        }

        public int RightOf(int index)
        {
            return RightOf(index, NodeIndex(index)).Item1;
        }

        private (int, int) LeftOf(int index, int nodeIndex)
        {
            if (nodeIndex == 0)
            {
                // First Node
                return index == 0 ? (-1, -1) : (_internal[nodeIndex].ID, nodeIndex);
            }

            Node left = _internal[nodeIndex - 1];
            Node node = _internal[nodeIndex];

            return index - 1 < left.Count ? (left.ID, nodeIndex - 1) : (node.ID, nodeIndex);
        }

        private (int, int) RightOf(int index, int nodeIndex)
        {
            if (nodeIndex == CompressedLength - 1)
            {
                // Last Node
                return index == Length - 1 ? (-1, -1) : (_internal[nodeIndex].ID, nodeIndex);
            }

            Node right = _internal[nodeIndex + 1];
            Node node = _internal[nodeIndex];

            return index + 1 >= node.Count ? (right.ID, nodeIndex + 1) : (node.ID, nodeIndex);
        }

        private int BinarySearch(int index)
        {
            int min = 0;
            int max = _internal.Length;

            while (min <= max)
            {
                int mid = (max + min) / 2;
                int count = _internal[mid].Count;

                if (index == count) return mid + 1;

                if (index < count) max = mid - 1;
                else min = mid + 1;
            }

            return min;
        }

        public override string ToString()
        {
            StringBuilder sb = new($"Length: {Length}, Compressed: {CompressedLength}\n");

            foreach (Node node in _internal)
            {
                sb.AppendLine($"[Data: {node.ID}, Count: {node.Count}]");
            }

            return sb.ToString();
        }
    }
}