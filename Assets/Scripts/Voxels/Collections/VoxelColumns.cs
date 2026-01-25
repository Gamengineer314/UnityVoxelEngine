using System;
using System.IO;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Voxels.Collections {

    /// <summary>
    /// Array of voxels that contain generic data.
    /// The voxels are organized as sizeX * sizeZ columns of (y, data) pairs.
    /// </summary>
    /// <typeparam name="T">Voxel data type</typeparam>
    [BurstCompile]
    public readonly unsafe struct VoxelColumns<T> where T : unmanaged {
        public readonly int sizeX, sizeZ; // Size in the x and z dimensions
        internal readonly NativeArray<Voxel<T>> voxels; // All columns
        internal readonly NativeArray<int> startIndices; // [sizeX * sizeZ + 1] sized array giving the start index of each column


        /// <summary>
        /// Get voxel columns from an asset
        /// </summary>
        /// <param name="asset">Asset containing the voxels</param>
        public VoxelColumns(TextAsset asset) {
            byte[] bytes = asset.bytes;
            int offset = 0;
            sizeX = BitConverter.ToInt32(bytes, offset);
            sizeZ = BitConverter.ToInt32(bytes, offset + sizeof(int));
            int nVoxels = BitConverter.ToInt32(bytes, offset + 2 * sizeof(int));
            offset += 3 * sizeof(int);
            voxels = asset.GetData<byte>()
                .GetSubArray(offset, nVoxels * sizeof(Voxel<T>))
                .Reinterpret<Voxel<T>>(1);
            offset += nVoxels * sizeof(Voxel<T>);
            startIndices = asset.GetData<byte>()
                .GetSubArray(offset, (sizeX * sizeZ + 1) * sizeof(int))
                .Reinterpret<int>(1);
        }

        /// <summary>
        /// Create voxel columns from a height map
        /// </summary>
        /// <param name="map">Highest voxel in each column</param>
        public VoxelColumns(Native2DArray<Voxel<T>> map) {
            sizeX = map.sizeX;
            sizeZ = map.sizeY;
            FromHeightMap(in map, out voxels, out startIndices);
        }


        public void Dispose() {
            voxels.Dispose();
            startIndices.Dispose();
        }

        public bool Created => voxels.IsCreated;


        /// <summary>
        /// Get the data of a voxel
        /// </summary>
        /// <param name="x">x coordinate of the voxel</param>
        /// <param name="y">y coordinate of the voxel</param>
        /// <param name="z">z coordinate of the voxel</param>
        /// <returns>Data of the voxel if found, default otherwise</returns>
        public T GetVoxel(int x, int y, int z) {
            for (int i = startIndices[x + sizeX * z]; i < startIndices[x + sizeX * z + 1]; i++) {
                if (voxels[i].y == y) return voxels[i].data;
            }
            return default;
        }

        public T GetVoxel(int3 coords) => GetVoxel(coords.x, coords.y, coords.z);


        /// <summary>
        /// Get a column of voxels
        /// </summary>
        /// <param name="x">x coordinate of the column</param>
        /// <param name="z">z coordinate of the column</param>
        /// <returns>Enumerable of voxels</returns>
        public NativeArray<Voxel<T>> GetColumn(int x, int z) {
            int start = startIndices[x + sizeX * z];
            int length = startIndices[x + sizeX * z + 1] - start;
            return voxels.GetSubArray(start, length);
        }

        public NativeArray<Voxel<T>> GetColumn(int2 coords) => GetColumn(coords.x, coords.y);


        /// <summary>
        /// Get the lowest voxel in a column
        /// </summary>
        /// <param name="x">x coordinate of the column</param>
        /// <param name="z">z coordinate of the column</param>
        /// <returns>y coordinate of the voxel, int.MaxValue if no voxels in this column</returns>
        public int GetMin(int x, int z) {
            if (startIndices[x + sizeX * z] == startIndices[x + sizeX * z + 1]) return int.MaxValue;
            return voxels[startIndices[x + sizeX * z]].y;
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
            return voxels[startIndices[x + sizeX * z + 1] - 1].y;
        }

        public int GetMax(int2 coords) => GetMax(coords.x, coords.y);


        /// <summary>
        /// Write voxel columns to a file
        /// </summary>
        /// <param name="filePath">Path to the file</param>
        /// <param name="voxels">The voxels</param>
        public void Write(string filePath) {
            using FileStream file = File.OpenWrite(filePath);
            file.Write(BitConverter.GetBytes(sizeX));
            file.Write(BitConverter.GetBytes(sizeZ));
            file.Write(BitConverter.GetBytes(voxels.Length));
            file.Write(voxels.Reinterpret<byte>(sizeof(Voxel<T>)).ToArray());
            file.Write(startIndices.Reinterpret<byte>(sizeof(int)).ToArray());
        }


        [BurstCompile]
        private static void FromHeightMap(in Native2DArray<Voxel<T>> map, out NativeArray<Voxel<T>> voxels, out NativeArray<int> startIndices) {
            NativeList<Voxel<T>> voxelsList = new(Allocator.Persistent);
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

                    // Add voxels
                    startIndices[x + map.sizeX * z] = voxelsList.Length;
                    for (int y = minNeighbor + 1; y <= maxY; y++) {
                        voxelsList.Add(new(y, map[x, z].data));
                    }
                }
            }
            startIndices[map.sizeX * map.sizeY] = voxelsList.Length;

            voxels = voxelsList.ToArray(Allocator.Persistent);
            voxelsList.Dispose();
        }
    }



    /// <summary>
    /// (y, data) pair in a VoxelColumns struct
    /// </summary>
    /// <typeparam name="T">Data type</typeparam>
    public readonly struct Voxel<T> where T : unmanaged {
        public readonly int y;
        public readonly T data;

        public Voxel(int y, T data) {
            this.y = y;
            this.data = data;
        }
    }
    
}