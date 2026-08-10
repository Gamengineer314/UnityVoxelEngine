using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Unity.Collections {

    /// <summary>
    /// 2D array stored in a 1D NativeArray
    /// </summary>
    /// <typeparam name="T">Type of the elements in the array</typeparam>
    public struct Native2DArray<T> : IEnumerable<T>, IDisposable where T : unmanaged {
        private NativeArray<T> array;
        public readonly int2 size;


        public Native2DArray(int2 size, Allocator allocator, NativeArrayOptions options = NativeArrayOptions.ClearMemory) {
            array = new(size.x * size.y, allocator, options);
            this.size = size;
        }

        public Native2DArray(int sizeX, int sizeY, Allocator allocator, NativeArrayOptions options = NativeArrayOptions.ClearMemory) :
            this(new int2(sizeX, sizeY), allocator, options) {}

        public void Dispose() => array.Dispose();


        public T this[int2 coords] {
            readonly get {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                if (math.any(coords < 0 | coords >= size)) throw new IndexOutOfRangeException($"Coordinates {coords} are out of range of Native2DArray of size {size}");
#endif
                return array[coords.x + size.x * coords.y];
            }
            set {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                if (math.any(coords < 0 | coords >= size)) throw new IndexOutOfRangeException($"Coordinates {coords} are out of range of Native2DArray of size {size}");
#endif
                array[coords.x + size.x * coords.y] = value;
            }
        }

        public T this[int x, int y] {
            readonly get => this[new int2(x, y)];
            set => this[new int2(x, y)] = value;
        }


        public readonly NativeArray<T> Array => array;
        public readonly bool IsCreated => array.IsCreated;
        public readonly NativeArray<T>.Enumerator GetEnumerator() => array.GetEnumerator();
        readonly IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
        readonly IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();


        /// <summary>
        /// Transpose the array
        /// </summary>
        /// <param name="allocator">Allocator for the transposed array</param>
        /// <returns>The transposed array</returns>
        public readonly unsafe Native2DArray<T> Transpose(Allocator allocator) {
            Native2DArray<T> transposed = new(size.y, size.x, allocator);
            Unsafe2DArray<T>.Transpose(transposed.array.GetUnsafePtr(), array.GetUnsafePtr(), size);
            return transposed;
        }
    }

}