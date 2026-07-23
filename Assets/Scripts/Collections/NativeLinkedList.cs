using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Collections {
    
    /// <summary>
    /// Doubly linked list of items
    /// </summary>
    /// <typeparam name="T">Type of the items in the list</typeparam>
    public unsafe struct NativeLinkedList<T> : IDisposable, IEnumerable<T>
        where T : unmanaged {
        [NativeDisableUnsafePtrRestriction] private UnsafeLinkedList<T>* list;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private AtomicSafetyHandle m_Safety;
        private static readonly SharedStatic<int> s_staticSafetyId = SharedStatic<int>.GetOrCreate<NativeLinkedList<T>>();
#endif

        /// <summary>
        /// Number of items in the list
        /// </summary>
        public int Length {
            get {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
                return list->length;
            }
        }

        /// <summary>
        /// Whether memory is allocated for the collection
        /// </summary>
        public readonly bool IsCreated => list != null;

        /// <summary>
        /// Index of the first node, 0 if none
        /// </summary>
        public int First {
            get {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
                return list->First;
            }
        }

        /// <summary>
        /// Index of the last node, 0 if none
        /// </summary>
        public int Last {
            get { 
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
                return list->Last;
            }
        }


        public NativeLinkedList(AllocatorManager.AllocatorHandle allocator, int initialCapacity = 1) {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            m_Safety = CollectionHelper.CreateSafetyHandle(allocator);
            CollectionHelper.SetStaticSafetyId<NativeLinkedList<T>>(ref m_Safety, ref s_staticSafetyId.Data);
            AtomicSafetyHandle.SetBumpSecondaryVersionOnScheduleWrite(m_Safety, true);
            if (UnsafeUtility.IsNativeContainerType<T>()) AtomicSafetyHandle.SetNestedContainer(m_Safety, true);
#endif
            list = AllocatorManager.Allocate<UnsafeLinkedList<T>>(allocator);
            *list = new(allocator, initialCapacity);
        }

        public void Dispose() {
            if (!IsCreated) return;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckDeallocateAndThrow(m_Safety);
            AtomicSafetyHandle.Release(m_Safety);
#endif
            list->Dispose();
            AllocatorManager.Free(list->allocator, list);
            list = null;
        }

        /// <summary>
        /// Remove all nodes from the list
        /// </summary>
        public void Clear() {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
            list->Clear();
        }


        /// <summary>
        /// Reference to a node in the list
        /// </summary>
        /// <param name="index">Index of the node</param>
        /// <returns>The reference to the node</returns>
        public Node this[int index] {
            get {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
                return new((*list)[index]);
            }
        }

        /// <summary>
        /// Set the value of a node
        /// </summary>
        /// <param name="index">Index of the node</param>
        /// <param name="value">The value</param>
        public void SetValue(int index, T value) {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
            (*list)[index].value = value;
        }


        /// <summary>
        /// Add a value after a node
        /// </summary>
        /// <param name="index">Index of the node</param>
        /// <param name="value">The value</param>
        /// <returns>Reference to the new node</returns>
        public int AddAfter(int index, T value) {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
            return list->AddAfter(index, value);
        }


        /// <summary>
        /// Add a value before a node
        /// </summary>
        /// <param name="index">Index of the node</param>
        /// <param name="value">The value</param>
        /// <returns>Reference to the new node</returns>
        public int AddBefore(int index, T value) {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
            return list->AddBefore(index, value);
        }


        /// <summary>
        /// Remove a node
        /// </summary>
        /// <param name="index">Index of the node</param>
        public void Remove(int index) {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
            list->Remove(index);
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


        public readonly NativeEnumerator<T, UnsafeLinkedList<T>.Enumerator> GetEnumerator()
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            => new(list->GetEnumerator(), m_Safety);
#else
            => new(list->GetEnumerator());
#endif

        readonly IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
        readonly IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();


        /// <summary>
        /// Linked-list node
        /// </summary>
        public readonly struct Node {
            public readonly T value;
            public readonly int next; // Index of the next node, 0 if none
            public readonly int prev; // Index of the previous node, 0 if none

            public readonly bool HasNext => next != 0;
            public readonly bool HasPrev => prev != 0;

            public Node(UnsafeLinkedList<T>.Node node) {
                value = node.value;
                next = node.next;
                prev = node.prev;
            }
        }
    }

}