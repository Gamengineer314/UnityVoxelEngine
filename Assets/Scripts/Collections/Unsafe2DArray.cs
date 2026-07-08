using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Unity.Collections.LowLevel.Unsafe {

    /// <summary>
    /// 2D array stored in a 1D NativeArray
    /// </summary>
    /// <typeparam name="T">Type of the elements in the array</typeparam>
    public struct Unsafe2DArray<T> : IEnumerable<T>, IDisposable where T : unmanaged {
        private UnsafeArray<T> array;
        public readonly int sizeX, sizeY; // Size in the x and y dimensions

        public Unsafe2DArray(int sizeX, int sizeY, Allocator allocator, NativeArrayOptions options = NativeArrayOptions.ClearMemory) {
            array = new(sizeX * sizeY, allocator, options);
            this.sizeX = sizeX;
            this.sizeY = sizeY;
        }

        public readonly void Dispose() => array.Dispose();

        public ref T this[int x, int y] {
            get {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                if (x < 0 || x >= sizeX) throw new IndexOutOfRangeException($"X coordinate {x} is out of range of Native2DArray of sizeX {sizeX}");
                if (y < 0 || y >= sizeY) throw new IndexOutOfRangeException($"Y coordinate {y} is out of range of Native2DArray of sizeY {sizeY}");
#endif
                return ref array[x + sizeX * y];
            }
        }

        public ref T this[int2 coords] => ref this[coords.x, coords.y];

        public readonly UnsafeArray<T> Array => array;
        public readonly UnsafeArray<T>.Enumerator GetEnumerator() => array.GetEnumerator();
        readonly IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
        readonly IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

}