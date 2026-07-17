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
    public readonly unsafe struct VoxelColumns {
        public readonly int sizeX, sizeZ; // Size in the x and z dimensions
        internal readonly NativeArray<Column> columns; // All columns
        internal readonly NativeArray<int> startIndices; // [sizeX * sizeZ + 1] sized array giving the start index of each column


        /// <summary>
        /// Get voxel columns from an asset
        /// </summary>
        /// <param name="asset">Asset containing the voxels</param>
        internal VoxelColumns(TextAsset asset) {
            byte[] bytes = asset.bytes;
            int offset = 0;
            sizeX = BitConverter.ToInt32(bytes, offset);
            sizeZ = BitConverter.ToInt32(bytes, offset + sizeof(int));
            int nVoxels = BitConverter.ToInt32(bytes, offset + 2 * sizeof(int));
            offset += 3 * sizeof(int);
            columns = asset.GetData<byte>()
                .GetSubArray(offset, nVoxels * sizeof(Column))
                .Reinterpret<Column>(1);
            offset += nVoxels * sizeof(Column);
            startIndices = asset.GetData<byte>()
                .GetSubArray(offset, (sizeX * sizeZ + 1) * sizeof(int))
                .Reinterpret<int>(1);
        }

        /// <summary>
        /// Create voxel columns from a height map
        /// </summary>
        /// <param name="map">Highest voxel in each column</param>
        public VoxelColumns(Native2DArray<Voxel> map) {
            sizeX = map.sizeX;
            sizeZ = map.sizeY;
            FromHeightMap(in map, out columns, out startIndices);
        }

        /// <summary>
        /// Create voxel columns from a 3D color array
        /// </summary>
        /// <param name="colors">Color of each voxel</param>
        public VoxelColumns(Native3DArray<Color32> colors) {
            sizeX = colors.sizeX;
            sizeZ = colors.sizeZ;
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
            int start = startIndices[x + sizeX * z];
            int len = startIndices[x + sizeX * z + 1] - start;
            while (len > 1) {
                int half = len >> 1;
                int middle = start + half;
                if (columns[middle].start > y) {
                    len = half;
                }
                else {
                    start = middle;
                    len -= half;
                }
            }
            Column column = columns[start];
            return column.start <= y && column.start + column.height > y ? column.color : default;
        }

        public Color32 GetVoxel(int3 coords) => GetVoxel(coords.x, coords.y, coords.z);


        /// <summary>
        /// Get a column of voxels
        /// </summary>
        /// <param name="x">x coordinate of the column</param>
        /// <param name="z">z coordinate of the column</param>
        /// <returns>Enumerable of voxels</returns>
        public Enumerable<Voxel, Enumerator> GetColumn(int x, int z) {
            int start = startIndices[x + sizeX * z];
            int length = startIndices[x + sizeX * z + 1] - start;
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
            if (startIndices[x + sizeX * z] == startIndices[x + sizeX * z + 1]) return int.MaxValue;
            return columns[startIndices[x + sizeX * z]].start;
        }

        public int GetMin(int2 coords) => GetMin(coords.x, coords.y);


        /// <summary>
        /// Get the highest voxel in a column
        /// </summary>
        /// <param name="x">x coordinate of the column</param>
        /// <param name="z">z coordinate of the column</param>
        /// <returns>y coordinate of the voxel, int.MinValue if no voxels in this column</returns>
        public int GetMax(int x, int z) {
            if (startIndices[x + sizeX * z] == startIndices[x + sizeX * z + 1]) return int.MinValue;
            Column column = columns[startIndices[x + sizeX * z + 1] - 1];
            return column.start + column.height - 1;
        }

        public int GetMax(int2 coords) => GetMax(coords.x, coords.y);


        /// <summary>
        /// Write voxel columns to a file
        /// </summary>
        /// <param name="filePath">Path to the file</param>
        public void Write(string filePath) {
            using FileStream file = File.Create(filePath);
            file.Write(BitConverter.GetBytes(sizeX));
            file.Write(BitConverter.GetBytes(sizeZ));
            file.Write(BitConverter.GetBytes(columns.Length));
            file.Write(columns.Reinterpret<byte>(sizeof(Column)).ToArray());
            file.Write(startIndices.Reinterpret<byte>(sizeof(int)).ToArray());
        }


        [BurstCompile]
        private static void FromHeightMap(in Native2DArray<Voxel> map, out NativeArray<Column> columns, out NativeArray<int> startIndices) {
            NativeList<Column> columnsList = new(Allocator.Temp);
            startIndices = new(map.sizeX * map.sizeY + 1, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            for (int z = 0; z < map.sizeY; z++) {
                for (int x = 0; x < map.sizeX; x++) {
                    // Find lowest highest voxel in neighbor columns
                    int maxY = map[x, z].y;
                    int minNeighbor = maxY - 1;
                    if (x > 0) minNeighbor = math.min(minNeighbor, map[x - 1, z].y);
                    if (x < map.sizeX - 1) minNeighbor = math.min(minNeighbor, map[x + 1, z].y);
                    if (z > 0) minNeighbor = math.min(minNeighbor, map[x, z - 1].y);
                    if (z < map.sizeY - 1) minNeighbor = math.min(minNeighbor, map[x, z + 1].y);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    if (maxY < 0 || maxY > ushort.MaxValue || minNeighbor < 0 || minNeighbor > ushort.MaxValue)
                        throw new ArgumentOutOfRangeException($"Height must be between 0 and {ushort.MaxValue}");
#endif

                    // Add voxels
                    startIndices[x + map.sizeX * z] = columnsList.Length;
                    columnsList.Add(new Column((ushort)(minNeighbor + 1), (ushort)(maxY - minNeighbor), map[x, z].color));
                }
            }
            startIndices[map.sizeX * map.sizeY] = columnsList.Length;

            columns = columnsList.ToArray(Allocator.Persistent);
            columnsList.Dispose();
        }


        [BurstCompile]
        private static void FromColorArray(in Native3DArray<Color32> colors, out NativeArray<Column> columns, out NativeArray<int> startIndices) {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (colors.sizeY > ushort.MaxValue)
                throw new ArgumentOutOfRangeException($"Y size must be between 0 and {ushort.MaxValue}");
#endif
            NativeList<Column> columnsList = new(Allocator.Temp);
            startIndices = new(colors.sizeX * colors.sizeZ + 1, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            for (int z = 0; z < colors.sizeZ; z++) {
                for (int x = 0; x < colors.sizeX; x++) {
                    startIndices[x + colors.sizeX * z] = columnsList.Length;
                    int start = -1;
                    for (int y = 0; y < colors.sizeY; y++) {
                        Color32 color = colors[x, y, z];
                        if (Voxel.Color32Equals(color, default)) continue;
                        if (start == -1) start = y;
                        if (y + 1 == colors.sizeY || !Voxel.Color32Equals(color, colors[x, y + 1, z])) {
                            columnsList.Add(new Column((ushort)start, (ushort)(y - start + 1), color));
                        }
                    }
                }
            }
            startIndices[colors.sizeX * colors.sizeZ] = columnsList.Length;

            columns = columnsList.ToArray(Allocator.Persistent);
            columnsList.Dispose();
        }


        /// <summary>
        /// Column of voxels with the same color
        /// </summary>
        internal readonly struct Column {
            public readonly ushort start;
            public readonly ushort height;
            public readonly Color32 color;

            public Column(ushort start, ushort height, Color32 color) {
                this.start = start;
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
                if (i == -1 || y == columns[i].start + columns[i].height - 1) {
                    i++;
                    if (i == columns.Length) return false;
                    y = columns[i].start;
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

        public Voxel(int y, Color32 color) {
            this.y = y;
            this.color = color;
        }

        public static bool Color32Equals(Color32 x, Color32 y)
            => x.r == y.r && x.g == y.g && x.b == y.b && x.a == y.a;
    }
    
}