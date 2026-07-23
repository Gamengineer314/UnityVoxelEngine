using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Collections {

    /// <summary>
    /// Queue of ordered items.
    /// The minimum item is dequeued first.
    /// </summary>
    /// <typeparam name="T">Type of the items</typeparam>
    [NativeContainer]
    public unsafe struct NativePriorityQueue<T> : IDisposable, IEnumerable<T>
        where T : unmanaged, IComparable<T> {
        [NativeDisableUnsafePtrRestriction] private UnsafePriorityQueue<T>* queue;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private AtomicSafetyHandle m_Safety;
        private static readonly SharedStatic<int> s_staticSafetyId = SharedStatic<int>.GetOrCreate<NativePriorityQueue<T>>();
#endif

        /// <summary>
        /// Number of items in the queue
        /// </summary>
        public int Length {
            get {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
                return queue->length;
            }
        }

        /// <summary>
        /// Whether memory is allocated for the collection
        /// </summary>
        public readonly bool IsCreated => queue != null;

        /// <summary>
        /// Minimum item in the queue
        /// </summary>
        public T First {
            get {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
                return queue->First;
            }
        }


        public NativePriorityQueue(Allocator allocator, int initialCapacity = 1) {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            m_Safety = CollectionHelper.CreateSafetyHandle(allocator);
            CollectionHelper.SetStaticSafetyId<NativePriorityQueue<T>>(ref m_Safety, ref s_staticSafetyId.Data);
            AtomicSafetyHandle.SetBumpSecondaryVersionOnScheduleWrite(m_Safety, true);
            if (UnsafeUtility.IsNativeContainerType<T>()) AtomicSafetyHandle.SetNestedContainer(m_Safety, true);
#endif
            queue = AllocatorManager.Allocate<UnsafePriorityQueue<T>>(allocator);
            *queue = new(allocator, initialCapacity);
        }

        public void Dispose() {
            if (!IsCreated) return;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckDeallocateAndThrow(m_Safety);
            AtomicSafetyHandle.Release(m_Safety);
#endif
            queue->Dispose();
            AllocatorManager.Free(queue->allocator, queue);
            queue = null;
        }
        
        /// <summary>
        /// Remove all items from the queue
        /// </summary>
        public void Clear() {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
            queue->Clear();
        }


        /// <summary>
        /// Add an item to the queue
        /// </summary>
        /// <param name="item">The item</param>
        public void Enqueue(T item) {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (queue->length + 1 == queue->capacity) AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
            else AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
            queue->Enqueue(item);
        }

        /// <summary>
        /// Remove the lowest item from the queue
        /// </summary>
        /// <returns>The item</returns>
        public T Dequeue() {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
            return queue->Dequeue();
        }


        /// <summary>
        /// Get a view to the items in the queue
        /// </summary>
        /// <returns>An array that points to the items</returns>
        public readonly NativeArray<T> AsArray() {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckGetSecondaryDataPointerAndThrow(m_Safety);
            AtomicSafetyHandle handle = m_Safety;
            AtomicSafetyHandle.UseSecondaryVersion(ref handle);
#endif
            NativeArray<T> array = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(queue->buffer, queue->length, Allocator.None);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref array, handle);
#endif
            return array;
        }

        public readonly NativeArray<T>.Enumerator GetEnumerator() {
            NativeArray<T> array = AsArray();
            return new NativeArray<T>.Enumerator(ref array);
        }

        readonly IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
        readonly IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

}