using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Unity.Collections {

    /// <summary>
    /// 3D array stored in a 1D NativeArray
    /// </summary>
    /// <typeparam name="T">Type of the elements in the array</typeparam>
    public struct Native3DArray<T> : IEnumerable<T>, IDisposable where T : struct {
        private NativeArray<T> array;
        public readonly int sizeX, sizeY, sizeZ; // Size in the x, y, and z dimensions

        public Native3DArray(int sizeX, int sizeY, int sizeZ, Allocator allocator, NativeArrayOptions options = NativeArrayOptions.ClearMemory) {
            array = new(sizeX * sizeY * sizeZ, allocator, options);
            this.sizeX = sizeX;
            this.sizeY = sizeY;
            this.sizeZ = sizeZ;
        }

        public void Dispose() => array.Dispose();

        public T this[int x, int y, int z] {
            readonly get {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                if (x < 0 || x >= sizeX) throw new IndexOutOfRangeException($"X coordinate {x} is out of range of Native3DArray of sizeX {sizeX}");
                if (y < 0 || y >= sizeY) throw new IndexOutOfRangeException($"Y coordinate {y} is out of range of Native3DArray of sizeY {sizeY}");
                if (z < 0 || z >= sizeZ) throw new IndexOutOfRangeException($"Z coordinate {z} is out of range of Native3DArray of sizeZ {sizeZ}");
#endif
                return array[x + sizeX * y + sizeX * sizeY * z];
            }
            set {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                if (x < 0 || x >= sizeX) throw new IndexOutOfRangeException($"X coordinate {x} is out of range of Native3DArray of sizeX {sizeX}");
                if (y < 0 || y >= sizeY) throw new IndexOutOfRangeException($"Y coordinate {y} is out of range of Native3DArray of sizeY {sizeY}");
                if (z < 0 || z >= sizeZ) throw new IndexOutOfRangeException($"Z coordinate {z} is out of range of Native3DArray of sizeZ {sizeZ}");
#endif
                array[x + sizeX * y + sizeX * sizeY * z] = value;
            }
        }

        public T this[int3 coords] {
            readonly get => this[coords.x, coords.y, coords.z];
            set => this[coords.x, coords.y, coords.z] = value;
        }

        public readonly NativeArray<T> Array => array;
        public readonly bool IsCreated => array.IsCreated;
        public readonly NativeArray<T>.Enumerator GetEnumerator() => array.GetEnumerator();
        readonly IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
        readonly IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

}