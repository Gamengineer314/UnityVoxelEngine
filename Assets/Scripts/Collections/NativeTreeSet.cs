using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Collections {

    /// <summary>
    /// Binary search tree containing a set of ordered item
    /// </summary>
    /// <typeparam name="T">Type of the items</typeparam>
    [NativeContainer]
    public unsafe struct NativeTreeSet<T> : IDisposable, IEnumerable<T>
        where T : unmanaged, IComparable<T> {
        [NativeDisableUnsafePtrRestriction] private UnsafeTreeSet<T>* tree;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private AtomicSafetyHandle m_Safety;
        private static readonly SharedStatic<int> s_staticSafetyId = SharedStatic<int>.GetOrCreate<NativeTreeSet<T>>();
#endif

        /// <summary>
        /// Number of items in the tree
        /// </summary>
        public int Length {
            get {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
                return tree->Length;
            }
        }
        

        public NativeTreeSet(AllocatorManager.AllocatorHandle allocator, int initialCapacity = 1) {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            m_Safety = CollectionHelper.CreateSafetyHandle(allocator);
            CollectionHelper.SetStaticSafetyId<NativeTreeSet<T>>(ref m_Safety, ref s_staticSafetyId.Data);
            AtomicSafetyHandle.SetBumpSecondaryVersionOnScheduleWrite(m_Safety, true);
            if (UnsafeUtility.IsNativeContainerType<T>())
                AtomicSafetyHandle.SetNestedContainer(m_Safety, true);
#endif
            tree = AllocatorManager.Allocate<UnsafeTreeSet<T>>(allocator);
            *tree = new(allocator, initialCapacity);
        }

        public void Dispose() {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckDeallocateAndThrow(m_Safety);
            AtomicSafetyHandle.Release(m_Safety);
#endif
            tree->Dispose();
            AllocatorManager.Free(tree->tree.allocator, tree);
            tree = null;
        }


        /// <summary>
        /// Check whether the set contains an item
        /// </summary>
        /// <param name="item">The item</param>
        /// <returns>Whether the set contains the item</returns>
        public readonly bool Contains(T item) {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
            return tree->Contains(item);
        }

        /// <summary>
        /// Add an item to the set if the item isn't already in the set
        /// </summary>
        /// <param name="item">The item</param>
        /// <returns>Whether the item was added</returns>
        public bool Add(T item) {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
            return tree->Add(item);
        }

        /// <summary>
        /// Remove an item from the set if the item is in the set
        /// </summary>
        /// <param name="item">The item</param>
        /// <returns>Whether the item was in the set</returns>
        public bool Remove(T item) {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
            return tree->Remove(item);   
        }

        /// <summary>
        /// Get the closest item in the set that is smaller or equal to a given item if there is one
        /// </summary>
        /// <param name="item">The item</param>
        /// <param name="floor">The closest item if found, default otherwise</param>
        /// <returns>Whether an item was found</returns>
        public readonly bool Floor(T item, out T floor) {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
            return tree->Floor(item, out floor);
        }

        /// <summary>
        /// Get the closest item in the set that is greater or equal to a given item if there is one
        /// </summary>
        /// <param name="item">The item</param>
        /// <param name="ceil">The closest item if found, default otherwise</param>
        /// <returns>Whether an item was found</returns>
        public readonly bool Ceil(T item, out T ceil) {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
            return tree->Ceil(item, out ceil);
        }


        public readonly NativeEnumerator<T, UnsafeTree<T, T>.Enumerator> GetEnumerator()
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            => new(tree->GetEnumerator(), m_Safety);
#else
            => new(tree->GetEnumerator());
#endif

        readonly IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
        readonly IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        
        /// <summary>
        /// Get a view of the sub-set containing all items before an item
        /// </summary>
        /// <param name="end">The item (exclusive)</param>
        /// <returns>Sub-set enumerable</returns>
        public readonly Enumerable<T, NativeEnumerator<T, UnsafeTree<T, T>.EnumeratorBefore>> SubSetBefore(T end)
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            => new(new(tree->SubSetBefore(end).GetEnumerator(), m_Safety));
#else
            => new(new(tree->SubSetBefore(end).GetEnumerator(), m_Safety));
#endif

        /// <summary>
        /// Get a view of the sub-set containing all items after an item
        /// </summary>
        /// <param name="start">The item (inclusive)</param>
        /// <returns>Sub-set enumerable</returns>
        public readonly Enumerable<T, NativeEnumerator<T, UnsafeTree<T, T>.EnumeratorAfter>> SubSetAfter(T start)
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            => new(new(tree->SubSetAfter(start).GetEnumerator(), m_Safety));
#else
            => new(new(tree->SubSetAfter(start).GetEnumerator(), m_Safety));
#endif

        /// <summary>
        /// Get a view of the sub-set containing all items in a range of items
        /// </summary>
        /// <param name="start">Start item (inclusive)</param>
        /// <param name="end">End item (exclusive)</param>
        /// <returns>Sub-set enumerable</returns>
        public readonly Enumerable<T, NativeEnumerator<T, UnsafeTree<T, T>.EnumeratorBetween>> SubSetBetween(T start, T end)
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            => new(new(tree->SubSetBetween(start, end).GetEnumerator(), m_Safety));
#else
            => new(new(tree->SubSetBetween(start, end).GetEnumerator(), m_Safety));
#endif
    }

}