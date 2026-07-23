using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Unity.Collections.LowLevel.Unsafe {
    
    /// <summary>
    /// Doubly linked list of items
    /// </summary>
    /// <typeparam name="T">Type of the items in the list</typeparam>
    public unsafe struct UnsafeLinkedList<T> : IDisposable, IEnumerable<T>
        where T : unmanaged {
        [NativeDisableUnsafePtrRestriction] public Node* buffer;
        public readonly AllocatorManager.AllocatorHandle allocator;
        public int length;
        public int capacity;
        public int reusable; // Index of the first reusable item, 0 if none

        /// <summary>
        /// Whether memory is allocated for the collection
        /// </summary>
        public readonly bool IsCreated => buffer != null;

        /// <summary>
        /// Index of the first node, 0 if none
        /// </summary>
        public int First => buffer[0].next;

        /// <summary>
        /// Index of the last node, 0 if none
        /// </summary>
        public int Last => buffer[0].prev;


        public UnsafeLinkedList(AllocatorManager.AllocatorHandle allocator, int initialCapacity = 1) {
            this.allocator = allocator;
            capacity = math.max(1, initialCapacity);
            buffer = AllocatorManager.Allocate<Node>(allocator, capacity);
            length = 0;
            reusable = 0;
            Clear();
        }

        public readonly void Dispose() => AllocatorManager.Free(allocator, buffer);

        /// <summary>
        /// Remove all nodes from the list
        /// </summary>
        public void Clear() {
            length = 0;
            reusable = 0;
            buffer[0].next = 0;
            buffer[0].prev = 0;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            for (int i = 0; i < capacity; i++) {
                buffer[i].prev = -1;
            }
#endif
        }


        /// <summary>
        /// Add a node
        /// </summary>
        /// <param name="node">The node</param>
        /// <returns>Index of the node</returns>
        public int Add(Node node) {
            int index;
            if (reusable != 0) {
                index = reusable;
                reusable = buffer[reusable].next;
            }
            else {
                if (length + 1 >= capacity) {
                    capacity *= 2;
                    Node* newBuffer = AllocatorManager.Allocate<Node>(allocator, capacity);
                    UnsafeUtility.MemCpy(newBuffer, buffer, length * sizeof(Node));
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    for (int i = length + 1; i < capacity; i++) {
                        buffer[i].prev = -1;
                    }
#endif
                    AllocatorManager.Free(allocator, buffer);
                    buffer = newBuffer;
                }
                index = length++;
            }
            return index;
        }


        /// <summary>
        /// Reference to a node in the list
        /// </summary>
        /// <param name="index">Index of the node</param>
        /// <returns>The reference to the node</returns>
        public ref Node this[int index] {
            get {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                if (index < 1 || index >= capacity || buffer[index].prev == -1) throw new ArgumentException("Invalid node index");
#endif
                return ref buffer[index];
            }
        }


        /// <summary>
        /// Add a value after a node
        /// </summary>
        /// <param name="index">Index of the node</param>
        /// <param name="value">The value</param>
        /// <returns>Reference to the new node</returns>
        public int AddAfter(int index, T value) {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (index < 0 || index >= capacity || buffer[index].prev == -1) throw new ArgumentException("Invalid node index");
#endif
            ref Node node = ref buffer[index];
            int newIndex = Add(new Node(value, index, node.next));
            buffer[node.next].prev = newIndex;
            node.next = newIndex;
            return newIndex;
        }


        /// <summary>
        /// Add a value before a node
        /// </summary>
        /// <param name="index">Index of the node</param>
        /// <param name="value">The value</param>
        /// <returns>Reference to the new node</returns>
        public int AddBefore(int index, T value) {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (index < 0 || index >= capacity || buffer[index].prev == -1) throw new ArgumentException("Invalid node index");
#endif
            ref Node node = ref buffer[index];
            int newIndex = Add(new Node(value, index, node.next));
            buffer[node.prev].next = newIndex;
            node.prev = newIndex;
            return newIndex;
        }


        /// <summary>
        /// Remove a node
        /// </summary>
        /// <param name="index">Index of the node</param>
        public void Remove(int index) {
            ref Node node = ref this[index];
            buffer[node.prev].next = node.next;
            buffer[node.next].prev = node.prev;
            node.next = reusable;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            node.prev = -1;
#endif
            reusable = index;
        }


        /// <summary>
        /// Add a value at the start of the list
        /// </summary>
        /// <param name="value">The value</param>
        /// <returns>Reference to the new node</returns>
        public int AddFirst(T value) => AddBefore(First, value);

        /// <summary>
        /// Add a value at the end of the list
        /// </summary>
        /// <param name="value">The value</param>
        /// <returns>Reference to the new node</returns>
        public int AddLast(T value) => AddAfter(Last, value);

        /// <summary>
        /// Remove the node at the start of the list
        /// </summary>
        public void RemoveFirst() => Remove(First);

        /// <summary>
        /// Remove the node at the end of the list
        /// </summary>
        public void RemoveLast() => Remove(Last);


        public readonly Enumerator GetEnumerator() => new(buffer);
        readonly IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
        readonly IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();


        public struct Node {
            public T value;
            public int next; // Index of the next node, 0 if none
            public int prev; // Index of the previous node, 0 if none, -1 if this node is invalid

            public readonly bool HasNext => next != 0;
            public readonly bool HasPrev => prev != 0;

            public Node(T value, int next, int prev) {
                this.value = value;
                this.next = next;
                this.prev = prev;
            }
        }


        public struct Enumerator : IEnumerator<T> {
            [NativeDisableUnsafePtrRestriction] public readonly Node* buffer;
            public int index;

            public Enumerator(Node* buffer) {
                this.buffer = buffer;
                index = 0;
            }

            public T Current => buffer[index].value;
            object IEnumerator.Current => Current;

            public void Dispose() {}

            public bool MoveNext() {
                index = buffer[index].next;
                return index != 0;
            }

            public void Reset() => index = 0;
        }
    }

}