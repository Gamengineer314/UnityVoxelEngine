using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Jobs;
using Voxels.Collections;

namespace Voxels.Rendering {

    /// <summary>
    /// Mesh generator from voxel collections
    /// </summary>
    [BurstCompile]
    internal class MeshGenerator {
        private readonly GenerationParameters parameters;
        private readonly bool textured;

        private readonly LayerData layer;
        private readonly Dictionary<VoxelColumns, List<(MeshData data, JobHandle handle)>> jobs = new();

        public int JobCount => jobs.Sum(kv => kv.Value.Count);
        public int CompletedCount => jobs.Sum(kv => kv.Value.Count(j => j.handle.IsCompleted));


        /// <summary>
        /// Create a mesh generator
        /// </summary>
        /// <param name="textured">
        /// Whether the meshes should use a texture (=> faces contain a texture coordinate, greedy mesher can combine faces with different colors)
        /// or not (=> faces contain the color directly, greedy mesher can't combine faces with different colors)
        /// </param>
        /// <param name="parameters">Generation parameters</param>
        public MeshGenerator(LayerData layer, bool textured, GenerationParameters parameters) {
            if (parameters.chunkSize <= 0 || parameters.chunkSize > VoxelFace.maxSize)
                throw new ArgumentException($"Chunk size must be positive and can't exceed {VoxelFace.maxSize}", nameof(parameters.chunkSize));
            this.textured = textured;
            this.parameters = parameters;
            this.layer = layer;
        }
        

        /// <summary>
        /// Complete the generation jobs of a mesh and add their results
        /// </summary>
        /// <param name="voxels">Voxels that were passed to Schedule</param>
        /// <param name="startInstance">Index of the first instance of the mesh</param>
        public void Complete(VoxelColumns voxels, int startInstance) {
            if (!jobs.TryGetValue(voxels, out List<(MeshData data, JobHandle handle)> meshJobs)) return;
            foreach ((MeshData data, JobHandle handle) in meshJobs) {
                handle.Complete();
                AddData(data, startInstance);
                data.Dispose();
            }
            jobs.Remove(voxels);
        }

        /// <summary>
        /// Complete the generation jobs of a mesh that are completed and add their results
        /// </summary>
        /// <param name="voxels">Voxels that were passed to Schedule</param>
        /// <param name="startInstance">Index of the first instance of the mesh</param>
        /// <returns>Whether all remaining jobs for that mesh were completed</returns>
        public bool CompleteCompleted(VoxelColumns voxels, int startInstance) {
            if (!jobs.TryGetValue(voxels, out List<(MeshData data, JobHandle handle)> meshJobs)) return true;
            for (int i = 0; i < meshJobs.Count; i++) {
                if (meshJobs[i].handle.IsCompleted) {
                    meshJobs[i].handle.Complete();
                    MeshData data = meshJobs[i].data;
                    AddData(data, startInstance);
                    data.Dispose();
                    meshJobs.RemoveAtSwapBack(i);
                }
            }
            if (meshJobs.Count == 0) {
                jobs.Remove(voxels);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Complete all generation jobs without adding their results
        /// </summary>
        public void Dispose() {
            foreach (List<(MeshData, JobHandle)> meshJobs in jobs.Values) {
                foreach ((MeshData data, JobHandle handle) in meshJobs) {
                    handle.Complete();
                    data.Dispose();
                }
            }
            jobs.Clear();
        }


        /// <summary>
        /// Schedule generation of meshes from a voxel collection
        /// </summary>
        /// <param name="voxels">The voxels</param>
        /// <param name="offset">Offset to add to the positions</param>
        public void Schedule(VoxelColumns voxels, float3 offset) {
            if (jobs.ContainsKey(voxels)) throw new InvalidOperationException("Generation of this mesh has already been scheduled");
            List<(MeshData data, JobHandle handle)> meshJobs = new();
            jobs[voxels] = meshJobs;
            int nJobsX = (int)math.ceil((float)voxels.sizeX / parameters.jobHorizontalSize);
            int nJobsZ = (int)math.ceil((float)voxels.sizeZ / parameters.jobHorizontalSize);
            for (int jobZ = 0, i = 0; jobZ < nJobsZ; jobZ++) {
                for (int jobX = 0; jobX < nJobsX; jobX++, i++) {
                    int jobStartX = jobX * parameters.jobHorizontalSize;
                    int jobStartZ = jobZ * parameters.jobHorizontalSize;
                    int jobSizeX = math.min(parameters.jobHorizontalSize, voxels.sizeX - jobStartX);
                    int jobSizeZ = math.min(parameters.jobHorizontalSize, voxels.sizeZ - jobStartZ);
                    MeshData data = new(true);
                    GeneratorJob job = new(voxels, jobStartX, jobStartZ, jobSizeX, jobSizeZ, offset, parameters.chunkSize, parameters.mergeNormalsThreshold, parameters.seenFromAbove, textured, data);
                    JobHandle handle = job.Schedule();
                    meshJobs.Add((data, handle));
                }
            }
        }


        /// <summary>
        /// Add the result of a job to the layer
        /// </summary>
        /// <param name="layer">Layer data</param>
        /// <param name="data">Job output data</param>
        /// <param name="textured">Whether the meshes use a texture</param>
        private void AddData(MeshData data, int startInstance) {
            AddData(ref layer.cpu, in data, startInstance, textured);
            layer.gpu.chunks.AddRange(layer.cpu.chunks.AsArray().GetSubArray(layer.gpu.chunks.Length, layer.cpu.chunks.Length - layer.gpu.chunks.Length));
            layer.gpu.faces.AddRange(layer.cpu.faces.AsArray().GetSubArray(layer.gpu.faces.Length, layer.cpu.faces.Length - layer.gpu.faces.Length));
            layer.gpu.colors.AddRange(layer.cpu.colors.AsArray().GetSubArray(layer.gpu.colors.Length, layer.cpu.colors.Length - layer.gpu.colors.Length));
        }

        [BurstCompile]
        private static void AddData(ref CPULayerData layer, in MeshData data, int startInstance, bool textured) {
            // Increase capacity
            layer.faces.Capacity = layer.faces.Length + data.faces.Length;
            layer.chunks.Capacity = layer.chunks.Length + data.chunks.Length;
            if (textured) layer.colors.Capacity = layer.colors.Length + data.colors.Length;

            // Add chunks
            int startFace = layer.faces.Length;
            int startColor = layer.colors.Length;
            foreach (VoxelChunk chunk in data.chunks) {
                layer.chunks.Add(new VoxelChunk(
                    chunk.center, chunk.size, chunk.offset.position, chunk.offset.Color + startColor,
                    chunk.Normal, chunk.StartFace + startFace, chunk.FaceCount, startInstance, 0, 0
                ));
            }

            // Add faces and colors
            if (textured) {
                foreach (VoxelFace face in data.faces) {
                    layer.faces.Add(face);
                }
                foreach (Color32 color in data.colors) {
                    layer.colors.Add(color);
                }
            }
            else {
                // Merge colors
                UnsafeArray<int> colorMap = new(data.colors.Length, Allocator.Temp);
                foreach (KVPair<uint, int> kv in data.colorIndices) {
                    if (layer.colorIndices.TryGetValue(kv.Key, out int index)) {
                        colorMap[kv.Value] = index;
                    }
                    else {
                        colorMap[kv.Value] = layer.colorIndices.Count;
                        layer.colorIndices[kv.Key] = layer.colorIndices.Count;
                        layer.colors.Add(Identifiers.IdToColor(kv.Key));
                    }
                }
                if (data.colorIndices.Count > VoxelFace.maxColor) throw new InvalidOperationException($"Number of colors can't exceed {VoxelFace.maxColor}");

                foreach (VoxelFace face in data.faces) {
                    layer.faces.Add(new VoxelFace(face.Position, face.Width, face.Height, face.Normal, colorMap[face.Color]));
                }
                colorMap.Dispose();
            }
        }



        [BurstCompile]
        private struct GeneratorJob : IJob {
            private const int subChunkSize = 64;

            [ReadOnly] private readonly VoxelColumns voxels; // All voxels
            private readonly int startX, startZ; // Start of the part to generate
            private readonly int sizeX, sizeZ; // Size of the part to generate
            private readonly float3 offset;
            private readonly int chunkSize;
            private readonly int mergeNormalsThreshold;
            private readonly bool seenFromAbove;
            private readonly bool textured;
            private MeshData data;

            private int3 currentChunkStart;
            private int3 currentSubChunkStart;
            private int3 currentSubChunkSize;
            private int chunkIndex;
            private int startFace;
            private int nIDs;
            private UnsafeArray<ulong> rows;
            private UnsafeArray<bool2> sides;
            private UnsafeArray<ulong> planes;
            private UnsafeList<VoxelFace> currentFaces;
            private UnsafeArray<int2> chunks;

            public GeneratorJob(
                VoxelColumns voxels,
                int startX, int startZ, int sizeX, int sizeZ,
                float3 offset,
                int chunkSize, int mergeNormalsThreshold, bool seenFromAbove, bool textured,
                MeshData data
            ) {
                this.voxels = voxels;
                this.startX = startX;
                this.startZ = startZ;
                this.sizeX = sizeX;
                this.sizeZ = sizeZ;
                this.offset = offset;
                this.chunkSize = chunkSize;
                this.mergeNormalsThreshold = mergeNormalsThreshold;
                this.seenFromAbove = seenFromAbove;
                this.textured = textured;
                this.data = data;

                currentChunkStart = 0;
                currentSubChunkStart = 0;
                currentSubChunkSize = 0;
                startFace = 0;
                chunkIndex = 0;
                nIDs = 0;
                rows = default;
                sides = default;
                planes = default;
                currentFaces = default;
                chunks = default;
            }


            public void Execute() {
                // Find IDs and y ranges
                int nChunksX = (int)math.ceil((float)sizeX / chunkSize);
                int nChunksZ = (int)math.ceil((float)sizeZ / chunkSize);
                Native2DArray<int2> yRanges = new(nChunksX, nChunksZ, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                for (int chunkZ = 0; chunkZ < nChunksZ; chunkZ++) {
                    int chunkStartZ = startZ + chunkZ * chunkSize;
                    int chunkEndZ = math.min(chunkStartZ + chunkSize, startZ + sizeZ);
                    for (int chunkX = 0; chunkX < nChunksX; chunkX++) {
                        int chunkStartX = startX + chunkX * chunkSize;
                        int chunkEndX = math.min(chunkStartX + chunkSize, startX + sizeX);
                        int min = int.MaxValue, max = int.MinValue;
                        for (int z = chunkStartZ; z < chunkEndZ; z++) {
                            for (int x = chunkStartX; x < chunkEndX; x++) {
                                min = math.min(min, voxels.GetMin(x, z));
                                max = math.max(max, voxels.GetMax(x, z));
                                foreach (Voxel voxel in voxels.GetColumn(x, z)) {
                                    if (!textured) {
                                        uint id = Identifiers.ColorToId(voxel.color);
                                        if (!data.colorIndices.ContainsKey(id)) {
                                            data.colorIndices[id] = data.colors.Length;
                                            data.colors.Add(voxel.color);
                                            nIDs++;
                                        }
                                    }
                                }
                            }
                        }
                        yRanges[chunkX, chunkZ] = new(min, max);
                    }
                }
                if (textured) nIDs = 1;

                // Generate all chunks
                rows = new UnsafeArray<ulong>(subChunkSize * subChunkSize * 3, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                sides = new UnsafeArray<bool2>(subChunkSize * subChunkSize * 3, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                planes = new UnsafeArray<ulong>(subChunkSize * subChunkSize * nIDs * 6, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                currentFaces = new UnsafeList<VoxelFace>(0, Allocator.Temp);
                for (int chunkZ = 0; chunkZ < nChunksZ; chunkZ++) {
                    currentChunkStart.z = startZ + chunkZ * chunkSize;
                    int chunkEndZ = math.min(currentChunkStart.z + chunkSize, startZ + sizeZ);
                    int nSubChunksZ = (int)math.ceil((float)(chunkEndZ - currentChunkStart.z) / subChunkSize);
                    for (int chunkX = 0; chunkX < nChunksX; chunkX++) {
                        currentChunkStart.x = startX + chunkX * chunkSize;
                        int chunkEndX = math.min(currentChunkStart.x + chunkSize, startX + sizeX);
                        int nSubChunksX = (int)math.ceil((float)(chunkEndX - currentChunkStart.x) / subChunkSize);
                        int2 yRange = yRanges[chunkX, chunkZ];
                        int nChunksY = (int)math.ceil((float)(yRange.y - yRange.x) / chunkSize);
                        for (int chunkY = 0; chunkY < nChunksY; chunkY++) {
                            currentChunkStart.y = yRange.x + chunkY * chunkSize;
                            int chunkEndY = math.min(currentChunkStart.y + chunkSize, yRange.y);
                            int nSubChunksY = (int)math.ceil((float)(chunkEndY - currentChunkStart.y) / subChunkSize);

                            // Generate all chunks
                            chunks = new UnsafeArray<int2>(nSubChunksX * nSubChunksY * nSubChunksZ * 6, Allocator.Temp);
                            chunkIndex = 0;
                            for (int subChunkZ = 0; subChunkZ < nSubChunksZ; subChunkZ++) {
                                currentSubChunkStart.z = currentChunkStart.z + subChunkZ * subChunkSize;
                                currentSubChunkSize.z = math.min(subChunkSize, chunkEndZ - currentSubChunkStart.z);
                                for (int subChunkX = 0; subChunkX < nSubChunksX; subChunkX++) {
                                    currentSubChunkStart.x = currentChunkStart.x + subChunkX * subChunkSize;
                                    currentSubChunkSize.x = math.min(subChunkSize, chunkEndX - currentSubChunkStart.x);
                                    for (int subChunkY = 0; subChunkY < nSubChunksY; subChunkY++) {
                                        currentSubChunkStart.y = yRange.x + subChunkY * subChunkSize;
                                        currentSubChunkSize.y = math.min(subChunkSize, yRange.y + 1 - currentSubChunkStart.y);

                                        // Generate one chunk
                                        rows.Clear();
                                        sides.Clear();
                                        planes.Clear();
                                        GenerateBinarySolidBlocks();
                                        GenerateBinaryPlanes();
                                        GenerateOptimizedChunk();
                                        chunkIndex += 6;
                                    }
                                }
                            }

                            GenerateChunk();
                            currentFaces.Clear();
                        }
                    }
                }

                yRanges.Dispose();
                rows.Dispose();
                sides.Dispose();
                planes.Dispose();
                currentFaces.Dispose();
            }


            // rows: bit rows containing 1 if the block is solid, 0 otherwise
            // sides: 2 bools, first is true if the block before the row is solid, 0 otherwise, second is true if the block after the row is solid, 0 otherwise
            // rows and sides contain subChunkSize * subChunkSize elements for each axis (x, z, y)
            private void GenerateBinarySolidBlocks() {
                // Rows and y sides
                for (int z = 0; z < currentSubChunkSize.z; z++) {
                    for (int x = 0; x < currentSubChunkSize.x; x++) {
                        bool2 ySide = new(false, false);
                        foreach (Voxel voxel in voxels.GetColumn(currentSubChunkStart.x + x, currentSubChunkStart.z + z)) {
                            int y = voxel.y - currentSubChunkStart.y;
                            if (y >= 0 && y < currentSubChunkSize.y) {
                                rows[y + z * subChunkSize] |= 1UL << x; // x
                                rows[x + z * subChunkSize + subChunkSize * subChunkSize] |= 1UL << y; // y
                                rows[y + x * subChunkSize + 2 * subChunkSize * subChunkSize] |= 1UL << z; // z
                            }
                            else if (y == -1) ySide.x = true;
                            else if (y == subChunkSize) ySide.y = true;
                        }
                    }
                }

                // x and z sides
                if (currentSubChunkStart.x > 0) {
                    for (int z = 0; z < currentSubChunkSize.z; z++) {
                        foreach (Voxel voxel in voxels.GetColumn(currentSubChunkStart.x - 1, currentSubChunkStart.z + z)) {
                            int y = voxel.y - currentSubChunkStart.y;
                            if (y >= 0 && y < currentSubChunkSize.y) sides[y + z * subChunkSize].x = true;
                        }
                    }
                }
                if (currentSubChunkStart.x + currentSubChunkSize.x < voxels.sizeX) {
                    for (int z = 0; z < currentSubChunkSize.z; z++) {
                        foreach (Voxel voxel in voxels.GetColumn(currentSubChunkStart.x + currentSubChunkSize.x, currentSubChunkStart.z + z)) {
                            int y = voxel.y - currentSubChunkStart.y;
                            if (y >= 0 && y < currentSubChunkSize.y) sides[y + z * subChunkSize].y = true;
                        }
                    }
                }
                if (currentSubChunkStart.z > 0) {
                    for (int x = 0; x < currentSubChunkSize.x; x++) {
                        foreach (Voxel voxel in voxels.GetColumn(currentSubChunkStart.x + x, currentSubChunkStart.z - 1)) {
                            int y = voxel.y - currentSubChunkStart.y;
                            if (y >= 0 && y < currentSubChunkSize.y) sides[y + x * subChunkSize + 2 * subChunkSize * subChunkSize].x = true;
                        }
                    }
                }
                if (currentSubChunkStart.z + currentSubChunkSize.z < voxels.sizeZ) {
                    for (int x = 0; x < currentSubChunkSize.x; x++) {
                        foreach (Voxel voxel in voxels.GetColumn(currentSubChunkStart.x + x, currentSubChunkStart.z + currentSubChunkSize.z)) {
                            int y = voxel.y - currentSubChunkStart.y;
                            if (y >= 0 && y < currentSubChunkSize.y) sides[y + x * subChunkSize + 2 * subChunkSize * subChunkSize].y = true;
                        }
                    }
                }
            }


            // planes: 64 bits rows containing 1 if the face must be rendered, 0 otherwise
            // planes contains subChunkSize (rows in one plane) * subChunkSize (planes in one direction) * nIDs * 6 (x+, z+, y+, x-, z-, y-)
            private void GenerateBinaryPlanes() {
                GenerateAxisBinaryPlanes(0);
                GenerateAxisBinaryPlanes(1);
                GenerateAxisBinaryPlanes(2);
            }


            // Generate binary planes for one axis
            private void GenerateAxisBinaryPlanes(int axis) {
                int3 beforeX = currentSubChunkStart;
                for (int y = 0; y < currentSubChunkSize[VoxelNormals.HeightAxis(axis)]; y++) {
                    int3 pos = beforeX;
                    for (int x = 0; x < currentSubChunkSize[VoxelNormals.WidthAxis(axis)]; x++) {
                        ulong row = rows[x + y * subChunkSize + axis * subChunkSize * subChunkSize];
                        bool2 side = sides[x + y * subChunkSize + axis * subChunkSize * subChunkSize];

                        // Find faces to render in negative direction and add them to planes
                        ulong shiftedRow = row << 1;
                        if (side.x) shiftedRow |= 1;
                        ulong faceRow = row & ~shiftedRow;
                        while (faceRow != 0) {
                            int depth = math.tzcnt(faceRow);
                            faceRow &= ~(1UL << depth);
                            int3 posDepth = pos;
                            posDepth[axis] += depth;
                            if (seenFromAbove) { // Remove useless faces
                                int3 next = posDepth;
                                next[axis]--;
                                if (axis == 0 && next.x < 0) continue;
                                if (axis == 2 && next.z < 0) continue;
                                if (next.y < voxels.GetMin(next.xz)) continue;
                            }
                            int index = textured ? 0 : data.colorIndices[Identifiers.ColorToId(voxels.GetVoxel(posDepth))];
                            planes[y + depth * subChunkSize + index * subChunkSize * subChunkSize + 2 * axis * subChunkSize * subChunkSize * nIDs] |= 1UL << x;
                        }

                        // Find faces to render in positive direction and add them to planes
                        shiftedRow = row >> 1;
                        if (side.y) shiftedRow |= 1UL << 63;
                        faceRow = row & ~shiftedRow;
                        while (faceRow != 0) {
                            int depth = math.tzcnt(faceRow);
                            faceRow &= ~(1UL << depth);
                            int3 posDepth = pos;
                            posDepth[axis] += depth;
                            if (seenFromAbove) { // Remove useless faces
                                int3 next = posDepth;
                                next[axis]++;
                                if (axis == 0 && next.x >= voxels.sizeX) continue;
                                if (axis == 2 && next.z >= voxels.sizeZ) continue;
                                if (next.y < voxels.GetMin(next.xz)) continue;
                            }
                            int index = textured ? 0 : data.colorIndices[Identifiers.ColorToId(voxels.GetVoxel(posDepth))];
                            planes[y + depth * subChunkSize + index * subChunkSize * subChunkSize + (2 * axis + 1) * subChunkSize * subChunkSize * nIDs] |= 1UL << x;
                        }

                        pos[VoxelNormals.WidthAxis(axis)]++;
                    }
                    beforeX[VoxelNormals.HeightAxis(axis)]++;
                }
            }


            // Generate the chunk for each plane
            private void GenerateOptimizedChunk() {
                GenerateNormalOptimizedChunk(VoxelNormal.XNegative);
                GenerateNormalOptimizedChunk(VoxelNormal.XPositive);
                GenerateNormalOptimizedChunk(VoxelNormal.YNegative);
                GenerateNormalOptimizedChunk(VoxelNormal.YPositive);
                GenerateNormalOptimizedChunk(VoxelNormal.ZNegative);
                GenerateNormalOptimizedChunk(VoxelNormal.ZPositive);
            }


            // Generate the chunk for a normal
            private void GenerateNormalOptimizedChunk(VoxelNormal normal) {
                int startFace = currentFaces.Length;
                for (int i = 0; i < nIDs; i++) {
                    for (int depth = 0; depth < currentSubChunkSize[VoxelNormals.Axis(normal)]; depth++) {
                        GenerateOptimizedPlane(normal, depth, i);
                    }
                }
                chunks[chunkIndex + (int)normal] = new int2(startFace, currentFaces.Length);
            }


            // Generate the chunk for a plane and an ID
            private void GenerateOptimizedPlane(VoxelNormal normal, int depth, int index) {
                int startIndex = (int)normal * subChunkSize * subChunkSize * nIDs + index * subChunkSize * subChunkSize + depth * subChunkSize;
                int3 beforeX = currentSubChunkStart;
                beforeX[VoxelNormals.Axis(normal)] += depth;
                for (int y = 0; y < currentSubChunkSize[VoxelNormals.HeightAxis(normal)]; y++) {
                    int3 pos = beforeX;
                    ulong row = planes[startIndex + y];
                    int x = math.tzcnt(row);
                    pos[VoxelNormals.WidthAxis(normal)] += x;
                    while (x < currentSubChunkSize[VoxelNormals.WidthAxis(normal)]) {
                        int width = math.tzcnt(~(row >> x)); // Expand in x
                        ulong checkMask = (row << (64 - width - x)) >> (64 - width - x);
                        ulong deleteMask = ~checkMask;

                        int height = 1;
                        while (y + height < currentSubChunkSize[VoxelNormals.HeightAxis(normal)]) { // Expand in y
                            ref ulong nextRow = ref planes[startIndex + y + height];
                            if ((nextRow & checkMask) != checkMask) break;
                            nextRow &= deleteMask;
                            height++;
                        }

                        currentFaces.Add(new VoxelFace(pos - currentChunkStart, width, height, normal, index));

                        int prevX = x;
                        x += width;
                        x += math.tzcnt(row >> x);
                        pos[VoxelNormals.WidthAxis(normal)] += x - prevX;
                    }
                    beforeX[VoxelNormals.HeightAxis(normal)]++;
                }
            }


            // Split and merge sub-chunks and generate final output
            private void GenerateChunk() {
                int normalIndex = 0;
                int chunkIndex = 0;
                int faceIndex = 0;
                while (normalIndex < 6) {
                    int3 min = int.MaxValue;
                    int3 max = int.MinValue;
                    VoxelNormal chunkNormal = VoxelNormal.None;
                    int faceCount = 0;
                    int startColor = data.colors.Length;

                    // Add all faces of a chunk
                    while (faceCount < VoxelRenderer.maxFaceCount) {
                        if (faceIndex == chunks[chunkIndex].y) { // Next chunk
                            chunkIndex += 6;
                            if (chunkIndex >= chunks.length) { // Next normal
                                normalIndex++;
                                if (normalIndex == 6) break; // End
                                chunkIndex = normalIndex;
                                if (faceCount > 0 && currentFaces.Length > mergeNormalsThreshold) { // Generate chunk for this normal
                                    faceIndex = chunks[chunkIndex].x;
                                    break;
                                }
                            }
                            faceIndex = chunks[chunkIndex].x;
                            continue;
                        }

                        // Add face
                        VoxelFace face = currentFaces[faceIndex];
                        int3 pos = face.Position;
                        int width = face.Width;
                        int height = face.Height;
                        VoxelNormal normal = face.Normal;
                        if (textured) {
                            if (data.colors.Length - startColor + width * height - 1 > VoxelFace.maxColor) break;
                            data.faces.Add(new VoxelFace(pos, width, height, normal, data.colors.Length - startColor));
                            for (int x = 0; x < width; x++) {
                                for (int y = 0; y < height; y++) {
                                    int3 texturePos = currentChunkStart + pos;
                                    texturePos[VoxelNormals.WidthAxis(normal)] += x;
                                    texturePos[VoxelNormals.HeightAxis(normal)] += y;
                                    data.colors.Add(voxels.GetVoxel(texturePos));
                                }
                            }
                        }
                        else data.faces.Add(face);
                        if (chunkNormal == VoxelNormal.None) chunkNormal = normal;
                        else if (chunkNormal != normal) chunkNormal = VoxelNormal.Any;
                        int3 faceMin = currentChunkStart + pos;
                        if (VoxelNormals.Positive(normal)) faceMin[VoxelNormals.Axis(normal)]++;
                        int3 faceMax = faceMin;
                        faceMax[VoxelNormals.WidthAxis(normal)] += width;
                        faceMax[VoxelNormals.HeightAxis(normal)] += height;
                        min = math.select(min, faceMin, faceMin < min);
                        max = math.select(max, faceMax, faceMax > max);
                        faceCount++;
                        faceIndex++;
                    }

                    if (faceCount > 0) {
                        data.chunks.Add(new VoxelChunk(
                            offset + (float3)(max + min) / 2f, (float3)(max - min) / 2f, offset + currentChunkStart,
                            startColor, chunkNormal, startFace, faceCount, 0, 0, 0
                        ));
                        startFace += faceCount;
                    }
                }
            }
        }



        internal struct MeshData {
            public NativeList<VoxelFace> faces;
            public NativeList<VoxelChunk> chunks;
            public NativeList<Color32> colors;
            public NativeHashMap<uint, int> colorIndices;

            public MeshData(bool _) {
                faces = new(Allocator.Persistent);
                chunks = new(Allocator.Persistent);
                colors = new(Allocator.Persistent);
                colorIndices = new(0, Allocator.Persistent);
            }

            public void Dispose() {
                faces.Dispose();
                chunks.Dispose();
                colors.Dispose();
                colorIndices.Dispose();
            }
        }
    }



    /// <summary>
    /// Convert data to and from identifiers
    /// </summary>
    internal static class Identifiers {
        public static uint ColorToId(Color32 color) => color.r | (uint)color.g << 8 | (uint)color.b << 16 | (uint)color.a << 24;
        public static Color32 IdToColor(uint id) => new((byte)(id & 0xFF), (byte)(id >> 8 & 0xFF), (byte)(id >> 16 & 0xFF), (byte)(id >> 24 & 0xFF));
    }

}
