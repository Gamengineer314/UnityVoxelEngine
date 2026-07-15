using System;
using System.Collections;
using System.Collections.Generic;

namespace Unity.Collections.LowLevel.Unsafe {

    /// <summary>
    /// Unsafe array of items
    /// </summary>
    /// <typeparam name="T">Type of the items</typeparam>
    public readonly unsafe struct UnsafeArray<T> : IEnumerable<T>, IDisposable where T : unmanaged {
        [NativeDisableUnsafePtrRestriction] public readonly T* buffer;
        public readonly int length;
        public readonly AllocatorManager.AllocatorHandle allocator;


        public UnsafeArray(T* buffer, int length, AllocatorManager.AllocatorHandle allocator) {
            this.buffer = buffer;
            this.length = length;
            this.allocator = allocator;
        }

        public UnsafeArray(int length, AllocatorManager.AllocatorHandle allocator, NativeArrayOptions options = NativeArrayOptions.ClearMemory) :
            this(AllocatorManager.Allocate<T>(allocator, length), length, allocator) {
            if (options == NativeArrayOptions.ClearMemory) Clear();
        }

        public UnsafeArray(NativeArray<T> array) :
            this((T*)array.GetUnsafePtr(), array.Length, Allocator.None) {}

        public void Dispose() => AllocatorManager.Free(allocator, buffer);


        public ref T this[int index] {
            get {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                if (index < 0 || index >= length) throw new IndexOutOfRangeException($"Index {index} is out of range of UnsafeArray of size {length}");
#endif
                return ref buffer[index];
            }
        }


        public void Clear() {
            UnsafeUtility.MemClear(buffer, length * sizeof(T));
        }


        public Enumerator GetEnumerator() => new(this);
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();


        public struct Enumerator : IEnumerator<T> {
            [NativeDisableUnsafePtrRestriction] public readonly T* buffer;
            public int index;
            public readonly int length;

            public Enumerator(UnsafeArray<T> array) {
                buffer = array.buffer;
                index = -1;
                length = array.length;
            }

            public readonly T Current {
                get {
#if ENABLE_UNITY_COLLECTIONS_CHECKS 
                    if (index < 0 || index >= length) throw new InvalidOperationException($"Current access before MoveNext was called or after it returned false");
#endif
                    return buffer[index];
                }
            }

            readonly object IEnumerator.Current => Current;

            public readonly void Dispose() {}

            public bool MoveNext() => ++index < length;

            public void Reset() => index = 0;
        }
    }


    public static unsafe class UnsafeListExtensions {
        public static UnsafeArray<T> AsArray<T>(this UnsafeList<T> list) where T : unmanaged
            => new(list.Ptr, list.Length, Allocator.None);
    }

}