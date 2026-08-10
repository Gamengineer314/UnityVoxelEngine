using System;
using System.IO;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Burst;
using System.Collections.Generic;
using System.Collections;

namespace Voxels.Collections {

    /// <summary>
    /// Array of voxels that contain a color.
    /// The voxels are organized as sizeX * sizeZ columns of (y, color) pairs.
    /// </summary>
    [BurstCompile]
    public readonly unsafe struct VoxelColumns : IDisposable {
        public readonly int3 size;
        public readonly float3 offset; // Position offset
        internal readonly NativeArray<Column> columns; // All columns
        internal readonly NativeArray<int> startIndices; // [sizeX * sizeZ + 1] sized array giving the start index of each column


        internal VoxelColumns(int3 size, float3 offset, NativeArray<Column> columns, NativeArray<int> startIndices) {
            this.size = size;
            this.offset = offset;
            this.columns = columns;
            this.startIndices = startIndices;
        }
        
        /// <summary>
        /// Get voxel columns from a file
        /// </summary>
        /// <param name="path">File path</param>
        public VoxelColumns(string path) {
            byte[] array = File.ReadAllBytes(path);
            NativeArray<byte> nativeArray = new(array, Allocator.Persistent);
            int start = 0;
            size = new int3(
                BitConverter.ToInt32(array, start),
                BitConverter.ToInt32(array, start + sizeof(int)),
                BitConverter.ToInt32(array, start + 2 * sizeof(int))
            );
            int nVoxels = BitConverter.ToInt32(array, start + 3 * sizeof(int));
            start += 4 * sizeof(int);
            offset = new float3(
                BitConverter.ToSingle(array, start),
                BitConverter.ToSingle(array, start + sizeof(float)),
                BitConverter.ToSingle(array, start + 2 * sizeof(float))
            );
            start += 3 * sizeof(float);
            columns = nativeArray.GetSubArray(start, nVoxels * sizeof(Column)).Reinterpret<Column>(1);
            offset += nVoxels * sizeof(Column);
            startIndices = nativeArray.GetSubArray(start, (size.x * size.z + 1) * sizeof(int)).Reinterpret<int>(1);
        }

        /// <summary>
        /// Create voxel columns from a height map
        /// </summary>
        /// <param name="map">Highest voxel in each column</param>
        /// <param name="offset">Position offset</param>
        public VoxelColumns(Native2DArray<Voxel> map, float3 offset) {
            FromHeightMap(in map, out columns, out startIndices, out int sizeY, out int offsetY);
            size = new int3(map.size.x, sizeY, map.size.y);
            this.offset = new float3(offset.x, offset.y + offsetY, offset.z);
        }

        /// <summary>
        /// Create voxel columns from a 3D color array
        /// </summary>
        /// <param name="colors">Color of each voxel</param>
        /// <param name="offset">Position offset</param>
        public VoxelColumns(Native3DArray<Color32> colors, float3 offset) {
            size = colors.size;
            this.offset = offset;
            FromColorArray(in colors, out columns, out startIndices);
        }


        public void Dispose() {
            columns.Dispose();
            startIndices.Dispose();
        }

        public bool IsCreated => columns.IsCreated;


        /// <summary>
        /// Get the color of a voxel
        /// </summary>
        /// <param name="x">x coordinate of the voxel</param>
        /// <param name="y">y coordinate of the voxel</param>
        /// <param name="z">z coordinate of the voxel</param>
        /// <returns>Color of the voxel if found, default otherwise</returns>
        public Color32 GetVoxel(int x, int y, int z) {
            int start = startIndices[x + size.x * z];
            int len = startIndices[x + size.x * z + 1] - start;
            while (len > 1) {
                int half = len >> 1;
                int middle = start + half;
                if (columns[middle].min > y) {
                    len = half;
                }
                else {
                    start = middle;
                    len -= half;
                }
            }
            Column column = columns[start];
            return column.min <= y && column.Max >= y ? column.color : default;
        }

        public Color32 GetVoxel(int3 coords) => GetVoxel(coords.x, coords.y, coords.z);


        /// <summary>
        /// Get a column of voxels
        /// </summary>
        /// <param name="x">x coordinate of the column</param>
        /// <param name="z">z coordinate of the column</param>
        /// <returns>Enumerable of voxels</returns>
        public Enumerable<Voxel, Enumerator> GetColumn(int x, int z) {
            int start = startIndices[x + size.x * z];
            int length = startIndices[x + size.x * z + 1] - start;
            return new(new(columns.GetSubArray(start, length)));
        }

        public Enumerable<Voxel, Enumerator> GetColumn(int2 coords) => GetColumn(coords.x, coords.y);


        /// <summary>
        /// Get the lowest voxel in a column
        /// </summary>
        /// <param name="x">x coordinate of the column</param>
        /// <param name="z">z coordinate of the column</param>
        /// <returns>y coordinate of the voxel, int.MaxValue if no voxels in this column</returns>
        public int GetMin(int x, int z) {
            if (startIndices[x + size.x * z] == startIndices[x + size.x * z + 1]) return int.MaxValue;
            return columns[startIndices[x + size.x * z]].min;
        }

        public int GetMin(int2 coords) => GetMin(coords.x, coords.y);


        /// <summary>
        /// Get the highest voxel in a column
        /// </summary>
        /// <param name="x">x coordinate of the column</param>
        /// <param name="z">z coordinate of the column</param>
        /// <returns>y coordinate of the voxel, int.MinValue if no voxels in this column</returns>
        public int GetMax(int x, int z) {
            if (startIndices[x + size.x * z] == startIndices[x + size.x * z + 1]) return int.MinValue;
            return columns[startIndices[x + size.x * z + 1] - 1].Max;
        }

        public int GetMax(int2 coords) => GetMax(coords.x, coords.y);


        /// <summary>
        /// Write voxel columns to a file
        /// </summary>
        /// <param name="filePath">Path to the file</param>
        public void Write(string filePath) {
            using FileStream file = File.Create(filePath);
            file.Write(BitConverter.GetBytes(size.x));
            file.Write(BitConverter.GetBytes(size.y));
            file.Write(BitConverter.GetBytes(size.z));
            file.Write(BitConverter.GetBytes(columns.Length));
            file.Write(BitConverter.GetBytes(offset.x));
            file.Write(BitConverter.GetBytes(offset.y));
            file.Write(BitConverter.GetBytes(offset.z));
            file.Write(columns.Reinterpret<byte>(sizeof(Column)).ToArray());
            file.Write(startIndices.Reinterpret<byte>(sizeof(int)).ToArray());
        }


        [BurstCompile]
        private static void FromHeightMap(in Native2DArray<Voxel> map, out NativeArray<Column> columns, out NativeArray<int> startIndices, out int sizeY, out int offsetY) {
            NativeList<Column> columnsList = new(Allocator.Temp);
            startIndices = new(map.size.x * map.size.y + 1, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            // Find y bounds
            int min = int.MaxValue;
            int max = int.MinValue;
            for (int z = 0; z < map.size.y; z++) {
                for (int x = 0; x < map.size.x; x++) {
                    min = math.min(min, map[x, z].y);
                    max = math.max(max, map[x, z].y);
                }
            }
            sizeY = max - min + 1;
            offsetY = min;

            for (int z = 0; z < map.size.y; z++) {
                for (int x = 0; x < map.size.x; x++) {
                    // Find lowest highest voxel in neighbor columns
                    int maxY = map[x, z].y;
                    int minNeighbor = maxY - 1;
                    if (x > 0) minNeighbor = math.min(minNeighbor, map[x - 1, z].y);
                    if (x < map.size.x - 1) minNeighbor = math.min(minNeighbor, map[x + 1, z].y);
                    if (z > 0) minNeighbor = math.min(minNeighbor, map[x, z - 1].y);
                    if (z < map.size.y - 1) minNeighbor = math.min(minNeighbor, map[x, z + 1].y);
                    maxY -= offsetY;
                    minNeighbor -= offsetY;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    if (maxY < 0 || maxY > ushort.MaxValue || minNeighbor + 1 < 0 || minNeighbor + 1 > ushort.MaxValue)
                        throw new ArgumentOutOfRangeException($"Height must be between 0 and {ushort.MaxValue}");
#endif

                    // Add voxels
                    startIndices[x + map.size.x * z] = columnsList.Length;
                    columnsList.Add(new Column((ushort)(minNeighbor + 1), (ushort)(maxY - minNeighbor), map[x, z].color));
                }
            }
            startIndices[map.size.x * map.size.y] = columnsList.Length;

            columns = columnsList.ToArray(Allocator.Persistent);
            columnsList.Dispose();
        }


        [BurstCompile]
        private static void FromColorArray(in Native3DArray<Color32> colors, out NativeArray<Column> columns, out NativeArray<int> startIndices) {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (colors.size.y > ushort.MaxValue)
                throw new ArgumentOutOfRangeException($"Y size must be between 0 and {ushort.MaxValue}");
#endif
            NativeList<Column> columnsList = new(Allocator.Temp);
            startIndices = new(colors.size.x * colors.size.z + 1, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            for (int z = 0; z < colors.size.z; z++) {
                for (int x = 0; x < colors.size.x; x++) {
                    startIndices[x + colors.size.x * z] = columnsList.Length;
                    int start = 0;
                    for (int y = 0; y < colors.size.y; y++) {
                        Color32 color = colors[x, y, z];
                        if (Voxel.Color32Equals(color, default)) {
                            start = y + 1;
                            continue;
                        }
                        if (y + 1 == colors.size.y || !Voxel.Color32Equals(color, colors[x, y + 1, z])) {
                            columnsList.Add(new Column((ushort)start, (ushort)(y - start + 1), color));
                            start = y + 1;
                        }
                    }
                }
            }
            startIndices[colors.size.x * colors.size.z] = columnsList.Length;

            columns = columnsList.ToArray(Allocator.Persistent);
            columnsList.Dispose();
        }


        /// <summary>
        /// Column of voxels with the same color
        /// </summary>
        [Serializable]
        internal struct Column {
            public ushort min;
            public ushort height;
            public Color32 color;

            public readonly int Max => min + height - 1;

            public Column(ushort min, ushort height, Color32 color) {
                this.min = min;
                this.height = height;
                this.color = color;
            }
        }


        /// <summary>
        /// Column enumerator
        /// </summary>
        public struct Enumerator : IEnumerator<Voxel> {
            private readonly NativeArray<Column> columns;
            private int i;
            private int y;

            internal Enumerator(NativeArray<Column> columns) {
                this.columns = columns;
                i = -1;
                y = -1;    
            }

            public readonly Voxel Current {
                get {
#if ENABLE_UNITY_COLLECTIONS_CHECKS 
                    if (i < 0 || i >= columns.Length) throw new InvalidOperationException($"Current access before MoveNext was called or after it returned false");
#endif
                    return new Voxel(y, columns[i].color);
                }
            }

            readonly object IEnumerator.Current => Current;

            public readonly void Dispose() {}

            public bool MoveNext() {
#if ENABLE_UNITY_COLLECTIONS_CHECKS 
                if (i >= columns.Length) throw new InvalidOperationException($"MoveNext call after it already returned false");
#endif
                if (i == -1 || y == columns[i].min + columns[i].height - 1) {
                    i++;
                    if (i == columns.Length) return false;
                    y = columns[i].min;
                }
                else y++;
                return true;
            }

            public void Reset() {
                i = -1;
                y = -1;
            }
        }
    }



    /// <summary>
    /// (y, color) pair in a VoxelColumns struct
    /// </summary>
    public readonly struct Voxel {
        public readonly int y;
        public readonly Color32 color;

        public static readonly Color32 ghost = new(255, 0, 0, 0); // Block that isn't rendered and has no collider used to hide faces

        public Voxel(int y, Color32 color) {
            this.y = y;
            this.color = color;
        }

        public static bool Color32Equals(Color32 x, Color32 y)
            => x.r == y.r && x.g == y.g && x.b == y.b && x.a == y.a;

        public static int Color32HashCode(Color32 x)
            => x.r | x.g << 8 | x.b << 16 | x.a << 24;
    }
    
}