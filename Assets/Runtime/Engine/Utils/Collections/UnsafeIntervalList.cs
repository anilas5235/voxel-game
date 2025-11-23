using System;
using System.Text;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Runtime.Engine.Utils.Collections
{
    /// <summary>
    /// Speicher-effiziente Liste von komprimierten Intervallen (Run-Length Encoding ähnlich).
    /// Speichert Sequenzen gleicher IDs als Nodes mit End-Index. Ermöglicht O (log n) Zugriffe über Binärsuche.
    /// Unterstützt Coalescing beim Setzen zur Minimierung von Fragmentierung.
    /// </summary>
    [BurstCompile]
    public struct UnsafeIntervalList
    {
        // Array of structs impl
        /// <summary>
        /// Interner Node: repräsentiert ein Intervall von IDs bis inklusive End-Index (Count).
        /// </summary>
        private struct Node
        {
            /// <summary>
            /// Daten-ID für dieses Intervall.
            /// </summary>
            public ushort ID;
            /// <summary>
            /// Exklusiver End-Index (globale Länge bis hierher). Count dient gleichzeitig als kumulierte Länge.
            /// </summary>
            public int Count;

            public Node(ushort id, int count)
            {
                ID = id;
                Count = count;
            }
        }

        private UnsafeList<Node> _internal;

        // Struct of arrays impl (nicht genutzt, eventuell für zukünftiges Profiling behalten)
        // private UnsafeList<int> _Ids;
        // private UnsafeList<int> _Counts;

        /// <summary>
        /// Gesamtlänge der entpackten Daten (Summe aller Intervalllängen).
        /// </summary>
        public int Length;

        /// <summary>
        /// Aktuelle Anzahl komprimierter Nodes.
        /// </summary>
        public int CompressedLength => _internal.Length;

        /// <summary>
        /// Erstellt eine neue komprimierte Liste mit anfänglicher Kapazität.
        /// </summary>
        public UnsafeIntervalList(int capacity, Allocator allocator)
        {
            _internal = new UnsafeList<Node>(capacity, allocator);
            Length = 0;
        }

        /// <summary>
        /// Gibt interne native Speicher frei.
        /// </summary>
        public void Dispose()
        {
            _internal.Dispose();
        }

        /// <summary>
        /// Ermittelt Node Index für gegebenen entpackten Index (Binärsuche).
        /// </summary>
        public int NodeIndex(int index) => BinarySearch(index);

        /// <summary>
        /// Fügt neues Intervall hinzu (ID wiederholt sich <paramref name="count"/> mal).
        /// </summary>
        public void AddInterval(ushort id, int count)
        {
            Length += count;
            _internal.Add(new Node(id, Length));
        }

        /// <summary>
        /// Liefert Wert an entpackter Position.
        /// KOMPLEXITÄT: O(log n)
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">Wenn Index außerhalb (Editor/Development Builds).</exception>
        public ushort Get(int index)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (index >= Length)
                throw new IndexOutOfRangeException($"{index} is out of range for the given data of length {Length}");
#endif
            return _internal[BinarySearch(index)].ID;
        }

        /// <summary>
        /// Setzt Wert an entpackter Position mit umfangreicher Coalescing-Logik zur Minimierung der Node-Zahl.
        /// KOMPLEXITÄT: typ. O (log n), abhängig von Insert/Remove Range Operationen.
        /// </summary>
        /// <returns>True falls Änderung auftrat.</returns>
        /// <exception cref="IndexOutOfRangeException">Wenn Index außerhalb (Editor/Development Builds).</exception>
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

        /// <summary>
        /// Liefert linkes Nachbar-Element (ID) relativ zum Index.
        /// </summary>
        public int LeftOf(int index)
        {
            return LeftOf(index, NodeIndex(index)).Item1;
        }

        /// <summary>
        /// Liefert rechtes Nachbar-Element (ID) relativ zum Index.
        /// </summary>
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

        /// <summary>
        /// Binäre Suche nach Node für entpackten Index.
        /// </summary>
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

        /// <summary>
        /// Menschlich lesbare Darstellung für Debugging (Editor).
        /// </summary>
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