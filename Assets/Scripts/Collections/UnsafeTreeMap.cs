using System;
using System.Collections;
using System.Collections.Generic;

namespace Unity.Collections.LowLevel.Unsafe {

    /// <summary>
    /// Binary search tree containing a map of ordered key-value pairs
    /// </summary>
    /// <typeparam name="TKey">Type of the keys</typeparam>
    /// <typeparam name="TValue">Type of the values</typeparam>
    public unsafe struct UnsafeTreeMap<TKey, TValue> : IDisposable, IEnumerable<KeyValuePair<TKey, TValue>>
        where TKey : unmanaged, IComparable<TKey>
        where TValue : unmanaged {
        public struct ComparableKey : IComparable<KeyValuePair<TKey, TValue>> {
            public TKey key;

            public ComparableKey(TKey key) => this.key = key;

            public int CompareTo(KeyValuePair<TKey, TValue> o) => key.CompareTo(o.Key);
        }

        public UnsafeTree<ComparableKey, KeyValuePair<TKey, TValue>> tree;

        /// <summary>
        /// Number of items in the map
        /// </summary>
        public readonly int Length => tree.length;

        /// <summary>
        /// Whether memory is allocated for the collection
        /// </summary>
        public readonly bool IsCreated => tree.IsCreated;

        public UnsafeTreeMap(AllocatorManager.AllocatorHandle allocator, int initialCapacity = 1) {
            tree = new(allocator, initialCapacity);
        }

        public readonly void Dispose() => tree.Dispose();

        /// <summary>
        /// Remove all items from the map
        /// </summary>
        public void Clear() => tree.Clear();

        /// <summary>
        /// Get the value associated with a key in the map
        /// </summary>
        /// <param name="key">The key</param>
        /// <param name="value">The value if found, default otherwise</param>
        /// <returns>Whether the key was found</returns>
        public readonly bool TryGetValue(TKey key, out TValue value) {
            KeyValuePair<TKey, TValue>* pKV = tree.Ref(new(key));
            if (pKV == null) {
                value = default;
                return false;
            }
            else {
                value = pKV->Value;
                return true;
            }
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
            int length = Length;
            ref KeyValuePair<TKey, TValue> kv = ref tree.RefOrEmpty(new(key));
            if (Length != length) {
                newValue = value;
                kv = new(key, value);
                return true;
            }
            else {
                newValue = kv.Value;
                return false;
            }
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
                TryGetValue(key, out TValue value);
                return value;
            }
            set => tree.RefOrEmpty(new(key)) = new(key, value);
        }

        /// <summary>
        /// Remove a key-value pair from the map if the key is in the map
        /// </summary>
        /// <param name="key">The key</param>
        /// <param name="removed">The value if found, default otherwise</param>
        /// <returns>Whether the key was in the map</returns>
        public bool Remove(TKey key, out TValue removed) {
            KeyValuePair<TKey, TValue>* pKV = tree.Remove(new(key));
            if (pKV == null) {
                removed = default;
                return false;
            }
            else {
                removed = pKV->Value;
                return true;
            }
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
            KeyValuePair<TKey, TValue>* pFloor = tree.Floor(new(key));
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
        /// Get the key-value pair in the map with the closest key greater or equal to a given key if there is one
        /// </summary>
        /// <param name="key">The key</param>
        /// <param name="ceil">The key-value pair if found, default otherwise</param>
        /// <returns>Whether a key was found</returns>
        public readonly bool Ceil(TKey key, out KeyValuePair<TKey, TValue> ceil) {
            KeyValuePair<TKey, TValue>* pCeil = tree.Ceil(new(key));
            if (pCeil == null) {
                ceil = default;
                return false;
            }
            else {
                ceil = *pCeil;
                return true;
            }
        }

        public readonly UnsafeTree<ComparableKey, KeyValuePair<TKey, TValue>>.Enumerator GetEnumerator() => new(tree.buffer);
        readonly IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator() => GetEnumerator();
        readonly IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Get a view of the sub-map containing all key-value pairs before a key
        /// </summary>
        /// <param name="end">The key (exclusive)</param>
        /// <returns>Sub-map enumerable</returns>
        public readonly Enumerable<KeyValuePair<TKey, TValue>, UnsafeTree<ComparableKey, KeyValuePair<TKey, TValue>>.EnumeratorBefore> SubMapBefore(TKey end)
            => new(new(tree.buffer, new(end)));

        /// <summary>
        /// Get a view of the sub-map containing all key-value pairs after a key
        /// </summary>
        /// <param name="start">The key (inclusive)</param>
        /// <returns>Sub-map enumerable</returns>
        public readonly Enumerable<KeyValuePair<TKey, TValue>, UnsafeTree<ComparableKey, KeyValuePair<TKey, TValue>>.EnumeratorAfter> SubMapAfter(TKey start)
            => new(new(tree.buffer, new(start)));

        /// <summary>
        /// Get a view of the sub-map containing all key-value pairs in a range of keys
        /// </summary>
        /// <param name="start">Start key (inclusive)</param>
        /// <param name="end">End key (exclusive)</param>
        /// <returns>Sub-map enumerable</returns>
        public readonly Enumerable<KeyValuePair<TKey, TValue>, UnsafeTree<ComparableKey, KeyValuePair<TKey, TValue>>.EnumeratorBetween> SubMapBetween(TKey start, TKey end)
            => new(new(tree.buffer, new(start), new(end)));
    }

}