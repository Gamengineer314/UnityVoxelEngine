using System;
using System.Collections;
using System.Collections.Generic;

namespace Unity.Collections.LowLevel.Unsafe {

    /// <summary>
    /// Binary search tree containing a set of ordered item
    /// </summary>
    /// <typeparam name="T">Type of the items</typeparam>
    public unsafe struct UnsafeTreeSet<T> : IDisposable, IEnumerable<T>
        where T : unmanaged, IComparable<T> {
        public UnsafeTree<T, T> tree;

        /// <summary>
        /// Number of items in the set
        /// </summary>
        public readonly int Length => tree.length;

        /// <summary>
        /// Whether memory is allocated for the collection
        /// </summary>
        public readonly bool IsCreated => tree.IsCreated;

        public UnsafeTreeSet(AllocatorManager.AllocatorHandle allocator, int initialCapacity = 1) {
            tree = new(allocator, initialCapacity);
        }

        public readonly void Dispose() => tree.Dispose();

        /// <summary>
        /// Remove all items from the set
        /// </summary>
        public void Clear() => tree.Clear();

        /// <summary>
        /// Check whether the set contains an item
        /// </summary>
        /// <param name="item">The item</param>
        /// <returns>Whether the set contains the item</returns>
        public readonly bool Contains(T item) => tree.Ref(item) != null;

        /// <summary>
        /// Add an item to the set if the item isn't already in the set
        /// </summary>
        /// <param name="item">The item</param>
        /// <returns>Whether the item was added</returns>
        public bool Add(T item) {
            int length = Length;
            ref T addedItem = ref tree.RefOrEmpty(item);
            if (Length != length) {
                addedItem = item;
                return true;
            }
            else return false;
        }

        /// <summary>
        /// Remove an item from the set if the item is in the set
        /// </summary>
        /// <param name="item">The item</param>
        /// <returns>Whether the item was in the set</returns>
        public bool Remove(T item) => tree.Remove(item) != null;

        /// <summary>
        /// Get the closest item in the set that is smaller or equal to a given item if there is one
        /// </summary>
        /// <param name="item">The item</param>
        /// <param name="floor">The closest item if found, default otherwise</param>
        /// <returns>Whether an item was found</returns>
        public readonly bool Floor(T item, out T floor) {
            T* pFloor = tree.Floor(item);
            if (pFloor == null) {
                floor = default;
                return false;
            }
            else {
                floor = *pFloor;
                return true;
            }
        }

        /// <summary>
        /// Get the closest item in the set that is greater or equal to a given item if there is one
        /// </summary>
        /// <param name="item">The item</param>
        /// <param name="ceil">The closest item if found, default otherwise</param>
        /// <returns>Whether an item was found</returns>
        public readonly bool Ceil(T item, out T ceil) {
            T* pCeil = tree.Ceil(item);
            if (pCeil == null) {
                ceil = default;
                return false;
            }
            else {
                ceil = *pCeil;
                return true;
            }
        }

        public readonly UnsafeTree<T, T>.Enumerator GetEnumerator() => new(tree.buffer);
        readonly IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
        readonly IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Get a view of the sub-set containing all items before an item
        /// </summary>
        /// <param name="end">The item (exclusive)</param>
        /// <returns>Sub-set enumerable</returns>
        public readonly Enumerable<T, UnsafeTree<T, T>.EnumeratorBefore> SubSetBefore(T end)
            => new(new(tree.buffer, end));

        /// <summary>
        /// Get a view of the sub-set containing all items after an item
        /// </summary>
        /// <param name="start">The item (inclusive)</param>
        /// <returns>Sub-set enumerable</returns>
        public readonly Enumerable<T, UnsafeTree<T, T>.EnumeratorAfter> SubSetAfter(T start)
            => new(new(tree.buffer, start));

        /// <summary>
        /// Get a view of the sub-set containing all items in a range of items
        /// </summary>
        /// <param name="start">Start item (inclusive)</param>
        /// <param name="end">End item (exclusive)</param>
        /// <returns>Sub-set enumerable</returns>
        public readonly Enumerable<T, UnsafeTree<T, T>.EnumeratorBetween> SubSetBetween(T start, T end)
            => new(new(tree.buffer, start, end));
    }

}