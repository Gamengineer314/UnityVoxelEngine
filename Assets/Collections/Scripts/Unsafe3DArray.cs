using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Mathematics;

namespace Unity.Collections.LowLevel.Unsafe {

    /// <summary>
    /// 3D array stored in a 1D NativeArray
    /// </summary>
    /// <typeparam name="T">Type of the elements in the array</typeparam>
    [BurstCompile]
    public struct Unsafe3DArray<T> : IEnumerable<T>, IDisposable where T : unmanaged {
        private UnsafeArray<T> array;
        public readonly int3 size;


        public Unsafe3DArray(int3 size, AllocatorManager.AllocatorHandle allocator, NativeArrayOptions options = NativeArrayOptions.ClearMemory) {
            array = new(size.x * size.y * size.z, allocator, options);
            this.size = size;
        }

        public Unsafe3DArray(int sizeX, int sizeY, int sizeZ, AllocatorManager.AllocatorHandle allocator, NativeArrayOptions options = NativeArrayOptions.ClearMemory) :
            this(new int3(sizeX, sizeY, sizeZ), allocator, options) {}

        public readonly void Dispose() => array.Dispose();


        public ref T this[int3 coords] {
            get {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                if (math.any(coords < 0 | coords >= size)) throw new IndexOutOfRangeException($"Coordinates {coords} are out of range of Unsafe3DArray of size {size}");
#endif
                return ref array[coords.x + size.x * coords.y + size.x * size.y * size.z];
            }
        }

        public ref T this[int x, int y, int z] => ref this[new int3(x, y, z)];


        public readonly UnsafeArray<T> Array => array;
        public readonly UnsafeArray<T>.Enumerator GetEnumerator() => array.GetEnumerator();
        readonly IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
        readonly IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();


        /// <summary>
        /// Transpose the array
        /// </summary>
        /// <param name="newX">Index of the axis in this array that corresponds to the X axis in the transposed array</param>
        /// <param name="newY">Index of the axis in this array that corresponds to the Y axis in the transposed array</param>
        /// <param name="newZ">Index of the axis in this array that corresponds to the Z axis in the transposed array</param>
        /// <param name="allocator">Allocator for the transposed array</param>
        /// <returns>The transposed array</returns>
        public readonly unsafe Unsafe3DArray<T> Transpose(int newX, int newY, int newZ, AllocatorManager.AllocatorHandle allocator) {
            Unsafe3DArray<T> transposed = new(size[newX], size[newY], size[newZ], allocator);
            Transpose(transposed.array.buffer, array.buffer, size, newX, newY, newZ);
            return transposed;
        }

        [BurstCompile]
        public static unsafe void Transpose(void* dst, void* src, int3 srcSize, int newX, int newY, int newZ) {
            T* dstPtr = (T*)dst;
            T* srcPtr = (T*)src;
            int dstSizeX = srcSize[newX];
            int dstSizeY = srcSize[newY];
            for (int srcZ = 0; srcZ < srcSize.z; srcZ++) {
                for (int srcY = 0; srcY < srcSize.y; srcY++) {
                    for (int srcX = 0; srcX < srcSize.x; srcX++) {
                        int3 coords = new(srcX, srcY, srcZ);
                        int dstX = coords[newX];
                        int dstY = coords[newY];
                        int dstZ = coords[newZ];
                        dstPtr[dstX + dstY * dstSizeX + dstZ * dstSizeX * dstSizeY] = srcPtr[srcX + srcY * srcSize.x + srcZ * srcSize.x * srcSize.y];
                    }
                }
            }
        }
    }

}