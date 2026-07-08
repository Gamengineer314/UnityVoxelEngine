using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Collections {
    
    /// <summary>
    /// Binary search tree containing a map of ordered key-value pairs
    /// </summary>
    /// <typeparam name="TKey">Type of the keys</typeparam>
    /// <typeparam name="TValue">Type of the values</typeparam>
    [NativeContainer]
    public unsafe struct NativeTreeMap<TKey, TValue> : IDisposable, IEnumerable<KeyValuePair<TKey, TValue>>
        where TKey : unmanaged, IComparable<TKey>
        where TValue : unmanaged {
        [NativeDisableUnsafePtrRestriction] private UnsafeTreeMap<TKey, TValue>* tree;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private AtomicSafetyHandle m_Safety;
        private static readonly SharedStatic<int> s_staticSafetyId = SharedStatic<int>.GetOrCreate<NativeTreeMap<TKey, TValue>>();
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
        

        public NativeTreeMap(AllocatorManager.AllocatorHandle allocator, int initialCapacity = 1) {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            m_Safety = CollectionHelper.CreateSafetyHandle(allocator);
            CollectionHelper.SetStaticSafetyId<NativeTreeMap<TKey, TValue>>(ref m_Safety, ref s_staticSafetyId.Data);
            AtomicSafetyHandle.SetBumpSecondaryVersionOnScheduleWrite(m_Safety, true);
            if (UnsafeUtility.IsNativeContainerType<TKey>() || UnsafeUtility.IsNativeContainerType<TValue>())
                AtomicSafetyHandle.SetNestedContainer(m_Safety, true);
#endif
            tree = AllocatorManager.Allocate<UnsafeTreeMap<TKey, TValue>>(allocator);
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
        /// Get the value associated with a key in the map
        /// </summary>
        /// <param name="key">The key</param>
        /// <param name="value">The value if found, default otherwise</param>
        /// <returns>Whether the key was found</returns>
        public readonly bool TryGetValue(TKey key, out TValue value) {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
            return tree->TryGetValue(key, out value);
        }

        /// <summary>
        /// Check whether the map contains a key
        /// </summary>
        /// <param name="key">The key</param>
        /// <returns>Whether the map contains the key</returns>
        public readonly bool ContainsKey(TKey key) => TryGetValue(key, out _);

        /// <summary>
        /// Get the value associated with a key in the map
        /// </summary>
        /// <param name="key">The key</param>
        /// <param name="defaultValue">A default value</param>
        /// <returns>The value if the key is in the map, the default value otherwise</returns>
        public readonly TValue GetValueOrDefault(TKey key, TValue defaultValue = default)
            => TryGetValue(key, out TValue value) ? value : defaultValue;

        /// <summary>
        /// Add a key-value pair in the map if the key isn't already in the map
        /// </summary>
        /// <param name="key">The key</param>
        /// <param name="value">The value</param>
        /// <param name="newValue">Value associated with the key after the operation</param>
        /// <returns>Whether the key-value pair was added</returns>
        public bool TryAdd(TKey key, TValue value, out TValue newValue) {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
            return tree->TryAdd(key, value, out newValue);
        }

        /// <summary>
        /// Add a key-value pair in the map if the key isn't already in the map
        /// </summary>
        /// <param name="key">The key</param>
        /// <param name="value">The value</param>
        /// <returns>Whether the key-value pair was added</returns>
        public bool TryAdd(TKey key, TValue value) => TryAdd(key, value, out _);

        /// <summary>
        /// Get or set the value associated with a key.
        /// If the key wasn't in the map, the getter returns default and the setter adds it
        /// </summary>
        /// <param name="key">The key</param>
        public TValue this[TKey key] {
            readonly get {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
                if (!TryGetValue(key, out TValue value))
                    throw new KeyNotFoundException($"Key {key} not found");
#else
                TryGetValue(key, out TValue value);
#endif
                return value;
            }
            set {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
                (*tree)[key] = value;
            }
        }

        /// <summary>
        /// Remove a key-value pair from the map if the key is in the map
        /// </summary>
        /// <param name="key">The key</param>
        /// <param name="removed">The value if found, default otherwise</param>
        /// <returns>Whether the key was in the map</returns>
        public bool Remove(TKey key, out TValue removed) {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
            return tree->Remove(key, out removed);
        }

        /// <summary>
        /// Remove a key-value pair from the map if the key is in the map
        /// </summary>
        /// <param name="key">The key</param>
        /// <returns>Whether the key was in the map</returns>
        public bool Remove(TKey key) => Remove(key, out _);

        /// <summary>
        /// Get the key-value pair in the map with the closest key smaller or equal to a given key if there is one
        /// </summary>
        /// <param name="key">The key</param>
        /// <param name="floor">The key-value pair if found, default otherwise</param>
        /// <returns>Whether a key was found</returns>
        public readonly bool Floor(TKey key, out KeyValuePair<TKey, TValue> floor) {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
            return tree->Floor(key, out floor);
        }

        /// <summary>
        /// Get the key-value pair in the map with the closest key greater or equal to a given key if there is one
        /// </summary>
        /// <param name="key">The key</param>
        /// <param name="ceil">The key-value pair if found, default otherwise</param>
        /// <returns>Whether a key was found</returns>
        public readonly bool Ceil(TKey key, out KeyValuePair<TKey, TValue> ceil) {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
            return tree->Ceil(key, out ceil);
        }


        public readonly NativeEnumerator<KeyValuePair<TKey, TValue>, UnsafeTree<UnsafeTreeMap<TKey, TValue>.ComparableKey, KeyValuePair<TKey, TValue>>.Enumerator> GetEnumerator()
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            => new(tree->GetEnumerator(), m_Safety);
#else
            => new(tree->GetEnumerator());
#endif

        readonly IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator() => GetEnumerator();
        readonly IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Get a view of the sub-map containing all key-value pairs before a key
        /// </summary>
        /// <param name="end">The key (exclusive)</param>
        /// <returns>Sub-map enumerable</returns>
        public readonly Enumerable<KeyValuePair<TKey, TValue>, NativeEnumerator<KeyValuePair<TKey, TValue>, UnsafeTree<UnsafeTreeMap<TKey, TValue>.ComparableKey, KeyValuePair<TKey, TValue>>.EnumeratorBefore>> SubMapBefore(TKey end)
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            => new(new(tree->SubMapBefore(end).GetEnumerator(), m_Safety));
#else
            => new(new(tree->SubMapBefore(end).GetEnumerator()));
#endif

        /// <summary>
        /// Get a view of the sub-map containing all key-value pairs after a key
        /// </summary>
        /// <param name="start">The key (inclusive)</param>
        /// <returns>Sub-map enumerable</returns>
        public readonly Enumerable<KeyValuePair<TKey, TValue>, NativeEnumerator<KeyValuePair<TKey, TValue>, UnsafeTree<UnsafeTreeMap<TKey, TValue>.ComparableKey, KeyValuePair<TKey, TValue>>.EnumeratorAfter>> SubMapAfter(TKey start)
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            => new(new(tree->SubMapAfter(start).GetEnumerator(), m_Safety));
#else
            => new(new(tree->SubMapAfter(start).GetEnumerator()));
#endif

        /// <summary>
        /// Get a view of the sub-map containing all key-value pairs in a range of keys
        /// </summary>
        /// <param name="start">Start key (inclusive)</param>
        /// <param name="end">End key (exclusive)</param>
        /// <returns>Sub-map enumerable</returns>
        public readonly Enumerable<KeyValuePair<TKey, TValue>, NativeEnumerator<KeyValuePair<TKey, TValue>, UnsafeTree<UnsafeTreeMap<TKey, TValue>.ComparableKey, KeyValuePair<TKey, TValue>>.EnumeratorBetween>> SubMapBetween(TKey start, TKey end)
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            => new(new(tree->SubMapBetween(start, end).GetEnumerator(), m_Safety));
#else
            => new(new(tree->SubMapBetween(start, end).GetEnumerator()));
#endif
    }

}