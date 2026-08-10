using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Unity.Collections {

    /// <summary>
    /// 3D array stored in a 1D NativeArray
    /// </summary>
    /// <typeparam name="T">Type of the elements in the array</typeparam>
    public struct Native3DArray<T> : IEnumerable<T>, IDisposable where T : unmanaged {
        private NativeArray<T> array;
        public readonly int3 size; // Size in the x, y, and z dimensions


        public Native3DArray(int3 size, Allocator allocator, NativeArrayOptions options = NativeArrayOptions.ClearMemory) {
            array = new(size.x * size.y * size.z, allocator, options);
            this.size = size;
        }

        public Native3DArray(int sizeX, int sizeY, int sizeZ, Allocator allocator, NativeArrayOptions options = NativeArrayOptions.ClearMemory) :
            this(new int3(sizeX, sizeY, sizeZ), allocator, options) {}

        public void Dispose() => array.Dispose();


        public T this[int3 coords] {
            readonly get {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                if (math.any(coords < 0 | coords >= size)) throw new IndexOutOfRangeException($"Coordinates {coords} are out of range of Native3DArray of size {size}");
#endif
                return array[coords.x + size.x * coords.y + size.x * size.y * coords.z];
            }
            set {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                if (math.any(coords < 0 | coords >= size)) throw new IndexOutOfRangeException($"Coordinates {coords} are out of range of Native3DArray of size {size}");
#endif
                array[coords.x + size.x * coords.y + size.x * size.y * coords.z] = value;
            }
        }

        public T this[int x, int y, int z] {
            readonly get => this[new int3(x, y, z)];
            set => this[new int3(x, y, z)] = value;
        }


        public readonly NativeArray<T> Array => array;
        public readonly bool IsCreated => array.IsCreated;
        public readonly NativeArray<T>.Enumerator GetEnumerator() => array.GetEnumerator();
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
        public readonly unsafe Native3DArray<T> Transpose(int newX, int newY, int newZ, Allocator allocator) {
            Native3DArray<T> transposed = new(size[newX], size[newY], size[newZ], allocator);
            Unsafe3DArray<T>.Transpose(transposed.array.GetUnsafePtr(), array.GetUnsafePtr(), size, newX, newY, newZ);
            return transposed;
        }
    }

}