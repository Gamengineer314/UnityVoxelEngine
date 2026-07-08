using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Unity.Collections.LowLevel.Unsafe {

    /// <summary>
    /// Queue of ordered items.
    /// The minimum item is dequeued first.
    /// </summary>
    /// <typeparam name="T">Type of the items</typeparam>
    public unsafe struct UnsafePriorityQueue<T> : IDisposable, IEnumerable<T>
        where T : unmanaged, IComparable<T> {
        [NativeDisableUnsafePtrRestriction] public T* buffer;
        public readonly AllocatorManager.AllocatorHandle allocator;
        public int length;
        public int capacity;

        /// <summary>
        /// Minimum item in the queue
        /// </summary>
        public T First => buffer[1];


        public UnsafePriorityQueue(Allocator allocator, int initialCapacity = 1) {
            this.allocator = allocator;
            length = 0;
            capacity = math.max(1, initialCapacity);
            buffer = AllocatorManager.Allocate<T>(allocator, capacity);
        }

        public readonly void Dispose() => AllocatorManager.Free(allocator, buffer);

        /// <summary>
        /// Increment the length, doubling capacity if needed to add an item
        /// </summary>
        public void Grow() {
            if (++length >= capacity) {
                capacity *= 2;
                T* newBuffer = AllocatorManager.Allocate<T>(allocator, capacity);
                UnsafeUtility.MemCpy(newBuffer, buffer, length * sizeof(T));
                AllocatorManager.Free(allocator, buffer);
                buffer = newBuffer;
            }
        }


        /// <summary>
        /// Add an item in the queue
        /// </summary>
        /// <param name="item">The item</param>
        public void Enqueue(T item) {
            Grow();
            HeapifyUp(length, item);
        }

        /// <summary>
        /// Remove the lowest item from the queue
        /// </summary>
        /// <returns>The item</returns>
        public T Dequeue() {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (length <= 0) throw new InvalidOperationException("Dequeue from an empty queue");
#endif
            T item = buffer[1];
            HeapifyDown(1, buffer[length--]);
            return item;
        }


        /// <summary>
        /// Set an item at an index and maintain the invariant above it
        /// </summary>
        /// <param name="index">The index</param>
        /// <param name="item">The item</param>
        public void HeapifyUp(int index, T item) {
            while (index > 1) {
                int parentIndex = index >> 1;
                T parent = buffer[parentIndex];
                if (item.CompareTo(parent) >= 0) break;
                buffer[index] = parent;
                index = parentIndex;
            }
            buffer[index] = item;
        }

        /// <summary>
        /// Set an item at an index and maintain the invariant below it
        /// </summary>
        /// <param name="index">The index</param>
        /// <param name="item">The item</param>
        public void HeapifyDown(int index, T item) {
            int childIndex = index << 1;
            while (childIndex <= length) {
                T child = buffer[childIndex];
                if (childIndex + 1 <= length) {
                    T child2 = buffer[childIndex + 1];
                    if (child.CompareTo(child2) > 0) {
                        childIndex++;
                        child = child2;
                    }
                }
                if (item.CompareTo(child) <= 0) break;
                buffer[index] = child;
                index = childIndex;
                childIndex <<= 1;
            }
            buffer[index] = item;
        }


        /// <summary>
        /// Get a view to the items in the queue
        /// </summary>
        /// <returns>An array that points to the items</returns>
        public readonly UnsafeArray<T> AsArray() => new(buffer + 1, length, Allocator.None);

        public readonly UnsafeArray<T>.Enumerator GetEnumerator() => new(AsArray());
        readonly IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
        readonly IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

}