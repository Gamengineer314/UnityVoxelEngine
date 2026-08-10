using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Mathematics;

namespace Unity.Collections.LowLevel.Unsafe {

    /// <summary>
    /// 2D array stored in a 1D NativeArray
    /// </summary>
    /// <typeparam name="T">Type of the elements in the array</typeparam>
    [BurstCompile]
    public struct Unsafe2DArray<T> : IEnumerable<T>, IDisposable where T : unmanaged {
        private UnsafeArray<T> array;
        public readonly int2 size;


        public Unsafe2DArray(int2 size, AllocatorManager.AllocatorHandle allocator, NativeArrayOptions options = NativeArrayOptions.ClearMemory) {
            array = new(size.x * size.y, allocator, options);
            this.size = size;
        }

        public Unsafe2DArray(int sizeX, int sizeY, AllocatorManager.AllocatorHandle allocator, NativeArrayOptions options = NativeArrayOptions.ClearMemory) :
            this(new int2(sizeX, sizeY), allocator, options) {}

        public readonly void Dispose() => array.Dispose();


        public ref T this[int2 coords] {
            get {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                if (math.any(coords < 0 | coords >= size)) throw new IndexOutOfRangeException($"Coordinates {coords} are out of range of Unsafe2DArray of size {size}");
#endif
                return ref array[coords.x + size.x * coords.y];
            }
        }

        public ref T this[int x, int y] => ref this[new int2(x, y)];


        public readonly UnsafeArray<T> Array => array;
        public readonly UnsafeArray<T>.Enumerator GetEnumerator() => array.GetEnumerator();
        readonly IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
        readonly IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        
        
        /// <summary>
        /// Transpose the array
        /// </summary>
        /// <param name="allocator">Allocator for the transposed array</param>
        /// <returns>The transposed array</returns>
        public readonly unsafe Unsafe2DArray<T> Transpose(Allocator allocator) {
            Unsafe2DArray<T> transposed = new(size.y, size.x, allocator);
            Transpose(transposed.array.buffer, array.buffer, size);
            return transposed;
        }

        [BurstCompile]
        public static unsafe void Transpose(void* dst, void* src, int2 srcSize) {
            T* dstPtr = (T*)dst;
            T* srcPtr = (T*)src;
            for (int y = 0; y < srcSize.y; y++) {
                for (int x = 0; x < srcSize.x; x++) {
                    dstPtr[y + x * srcSize.y] = srcPtr[x + y * srcSize.x];
                }
            }
        }
    }

}