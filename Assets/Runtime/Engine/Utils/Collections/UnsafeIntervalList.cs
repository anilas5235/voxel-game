using System;
using System.Text;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Runtime.Engine.Utils.Collections
{
    /// <summary>
    /// Memory-efficient list of compressed intervals (similar to run-length encoding).
    /// Stores sequences of identical values as nodes with cumulative end index. Enables O(log n) lookup via binary search.
    /// Supports coalescing on Set to minimize fragmentation.
    /// </summary>
    [BurstCompile]
    public struct UnsafeIntervalList<T> where T : unmanaged, IEquatable<T>
    {
        /// <summary>
        /// Internal node representing an interval until inclusive end position (Count viewed as cumulative length).
        /// </summary>
        private struct Node
        {
            public T Value;
            public int Count;

            public Node(T value, int count)
            {
                Value = value;
                Count = count;
            }
        }

        private UnsafeList<Node> _internal;

        /// <summary>
        /// Total decompressed length (sum of interval lengths).
        /// </summary>
        public int Length;

        /// <summary>
        /// Current number of compressed nodes.
        /// </summary>
        public int CompressedLength => _internal.Length;

        /// <summary>
        /// Creates a new compressed list with initial capacity.
        /// </summary>
        public UnsafeIntervalList(int capacity, Allocator allocator)
        {
            _internal = new UnsafeList<Node>(capacity, allocator);
            Length = 0;
        }

        /// <summary>
        /// Disposes internal native memory.
        /// </summary>
        public void Dispose()
        {
            _internal.Dispose();
        }

        /// <summary>
        /// Finds node index for a decompressed index via binary search.
        /// </summary>
        public int NodeIndex(int index) => BinarySearch(index);

        /// <summary>
        /// Adds a new interval (value repeated <paramref name="count"/> times).
        /// </summary>
        public void AddInterval(T value, int count)
        {
            Length += count;
            _internal.Add(new Node(value, Length));
        }

        /// <summary>
        /// Gets value at decompressed position. Complexity: O(log n).
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">Wenn Index außerhalb (Editor/Development Builds).</exception>
        public T Get(int index)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if ((uint)index >= (uint)Length)
                throw new IndexOutOfRangeException($"{index} is out of range for the given data of length {Length}");
#endif
            return _internal[BinarySearch(index)].Value;
        }

        /// <summary>
        /// Sets value at decompressed position with coalescing logic. Complexity typically O(log n).
        /// Returns true if value changed.
        /// </summary>
        /// <returns>True falls Änderung auftrat.</returns>
        /// <exception cref="IndexOutOfRangeException">Wenn Index außerhalb (Editor/Development Builds).</exception>
        public bool Set(int index, T value)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if ((uint)index >= (uint)Length)
                throw new IndexOutOfRangeException($"{index} is out of range for the given data of length {Length}");
#endif

            int nodeIndex = BinarySearch(index);

            T current = _internal[nodeIndex].Value;
            if (current.Equals(value)) return false; // No Change

            (bool hasLeft, T leftItem, int leftNodeIndex) = LeftOf(index, nodeIndex);
            (bool hasRight, T rightItem, int rightNodeIndex) = RightOf(index, nodeIndex);

            bool eqLeft = hasLeft && value.Equals(leftItem);
            bool eqRight = hasRight && value.Equals(rightItem);

            // Nodes are returned by value, so we need to update them back in the array

            if (eqLeft && eqRight)
            {
                // [X,A,X] -> [X,X,X]
                Node leftNode = _internal[leftNodeIndex];
                leftNode.Count = _internal[rightNodeIndex].Count;
                _internal[leftNodeIndex] = leftNode;
                _internal.RemoveRange(nodeIndex, 2);
            }
            else if (eqLeft)
            {
                // [X,A,A,Y] -> [X,X,A,Y]
                Node leftNode = _internal[leftNodeIndex];
                Node node = _internal[nodeIndex];

                leftNode.Count++;
                _internal[leftNodeIndex] = leftNode;

                if (leftNode.Count == node.Count) _internal.RemoveRange(nodeIndex, 1); // [X,A,Y] -> [X,X,Y]
            }
            else if (eqRight)
            {
                // [X,A,A,Y] -> [X,A,Y,Y]
                Node leftNode = leftNodeIndex >= 0 ? _internal[leftNodeIndex] : default;
                Node node = _internal[nodeIndex];

                node.Count--;
                _internal[nodeIndex] = node;

                if (leftNodeIndex >= 0 && leftNode.Count == node.Count) _internal.RemoveRange(nodeIndex, 1); // [X,A,Y] -> [X,Y,Y]
            }
            else
            {
                // No Coalesce
                bool eqCurrentLeft = hasLeft && current.Equals(leftItem);
                bool eqCurrentRight = hasRight && current.Equals(rightItem);

                if (eqCurrentLeft && eqCurrentRight)
                {
                    // [X,X,X] -> [X,A,X]
                    _internal.InsertRange(nodeIndex, 2);

                    Node leftNode = _internal[nodeIndex];
                    Node node = _internal[nodeIndex + 1];
                    Node rightNode = _internal[nodeIndex + 2];

                    leftNode.Count = index;

                    node.Value = value;
                    node.Count = index + 1;

                    rightNode.Value = leftNode.Value;

                    _internal[nodeIndex] = leftNode;
                    _internal[nodeIndex + 1] = node;
                    _internal[nodeIndex + 2] = rightNode;
                }
                else if (!eqCurrentLeft && eqCurrentRight)
                {
                    // [X,Y,Y] -> [X,A,Y]
                    _internal.InsertRange(nodeIndex, 1);

                    Node node = _internal[nodeIndex];
                    node.Value = value;
                    node.Count = _internal[leftNodeIndex].Count + 1;
                    _internal[nodeIndex] = node;
                }
                else if (eqCurrentLeft)
                {
                    // [X,X,Y] -> [X,A,Y]
                    _internal.InsertRange(nodeIndex, 1);

                    Node node = _internal[nodeIndex + 1];
                    Node leftNode = _internal[leftNodeIndex];

                    node.Value = value;
                    node.Count = leftNode.Count;

                    leftNode.Count--;

                    _internal[nodeIndex + 1] = node;
                    _internal[leftNodeIndex] = leftNode;
                }
                else
                {
                    // [X,Y,X] -> [X,A,X] or [X,Y,Z] -> [X,A,Z]
                    Node node = _internal[nodeIndex];
                    node.Value = value;
                    _internal[nodeIndex] = node;
                }
            }

            return true;
        }

        /// <summary>
        /// Returns left neighbor item (Value) relative to index.
        /// </summary>
        public bool TryLeftOf(int index, out T value)
        {
            (bool has, T v, _) = LeftOf(index, NodeIndex(index));
            value = v;
            return has;
        }

        /// <summary>
        /// Returns right neighbor item (Value) relative to index.
        /// </summary>
        public bool TryRightOf(int index, out T value)
        {
            (bool has, T v, _) = RightOf(index, NodeIndex(index));
            value = v;
            return has;
        }

        private (bool hasValue, T value, int nodeIndex) LeftOf(int index, int nodeIndex)
        {
            if (nodeIndex == 0)
            {
                // First Node
                return index == 0 ? (false, default, -1) : (true, _internal[nodeIndex].Value, nodeIndex);
            }

            Node left = _internal[nodeIndex - 1];
            Node node = _internal[nodeIndex];

            return index - 1 < left.Count ? (true, left.Value, nodeIndex - 1) : (true, node.Value, nodeIndex);
        }

        private (bool hasValue, T value, int nodeIndex) RightOf(int index, int nodeIndex)
        {
            if (nodeIndex == CompressedLength - 1)
            {
                // Last Node
                return index == Length - 1 ? (false, default, -1) : (true, _internal[nodeIndex].Value, nodeIndex);
            }

            Node right = _internal[nodeIndex + 1];
            Node node = _internal[nodeIndex];

            return index + 1 >= node.Count ? (true, right.Value, nodeIndex + 1) : (true, node.Value, nodeIndex);
        }

        /// <summary>
        /// Binary search for node containing decompressed index.
        /// </summary>
        private int BinarySearch(int index)
        {
            int min = 0;
            int max = _internal.Length - 1;

            while (min <= max)
            {
                int mid = (max + min) >> 1;
                int count = _internal[mid].Count;

                if (index == count) return mid + 1;

                if (index < count) max = mid - 1;
                else min = mid + 1;
            }

            return min;
        }

        /// <summary>
        /// Human-readable representation for debugging (Editor).
        /// </summary>
        public override string ToString()
        {
            StringBuilder sb = new($"Length: {Length}, Compressed: {CompressedLength}\n");

            foreach (Node node in _internal)
            {
                sb.AppendLine($"[Data: {node.Value}, Count: {node.Count}]");
            }

            return sb.ToString();
        }

        public void Clear()
        {
            _internal.Clear();
            Length = 0;
        }
    }
}

