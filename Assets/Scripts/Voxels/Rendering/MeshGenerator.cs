using System;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Jobs;
using Unity.Collections.LowLevel.Unsafe;
using Voxels.Collections;
using System.Linq;

namespace Voxels.Rendering {

    /// <summary>
    /// Mesh generator from voxel collections
    /// </summary>
    internal abstract class MeshGenerator<Generator, Result>
        where Generator : unmanaged, IMeshGenerator
        where Result : unmanaged, IMeshResult<Generator> {
        private readonly int meshSize;
        private readonly int mergeNormalsThreshold;
        private readonly int jobHorizontalSize;
        private readonly bool seenFromAbove;

        protected Result result;
        private Unsafe2DArray<Generator> generators;
        private Native2DArray<JobHandle> handles;
        private JobHandle handle;

        public bool IsCompleted => handle.IsCompleted;
        public int JobCount => handles.Array.Length;
        public int CompletedCount => handles.Array.Count(h => h.IsCompleted);


        /// <summary>
        /// Create a mesh generator
        /// </summary>
        /// <param name="merger">
        /// Merger used to combine the result of multiple jobs and multiple generations.
        /// </param>
        /// <param name="meshSize">
        /// Max size for individual meshes.
        /// Multiple meshes can be generated from the same voxel collection if it exceeds this size.
        /// The generator will perform best if [meshSize] is a multiple of 64.
        /// </param>
        /// <param name="mergeNormalsThreshold">
        /// Number of faces below which meshes at the same position with different normals must be merged together.
        /// Objects smaller than the threshold will use a single mesh but can't be partially culled based on normals.
        /// </param>
        /// <param name="jobHorizontalSize">
        /// Max horizontal size a generator job can process.
        /// Multiple jobs will be used to generate the meshes in parallel if a voxel collection exceeds this size.
        /// The generator will perform best if [jobHorizontalSize] is a multiple of [meshSize]
        /// </param>
        /// <param name="seenFromAbove">
        /// Whether objects can only be seen from above and inside its horizontal bounds.
        /// This allows to remove faces below the objects and on their sides.
        /// </param>
        protected MeshGenerator(Result result, int meshSize = 64, int mergeNormalsThreshold = 256, int jobHorizontalSize = int.MaxValue, bool seenFromAbove = false) {
            if (meshSize > VoxelFace.maxSize) throw new ArgumentException($"Mesh size can't exceed {VoxelFace.maxSize}", nameof(meshSize));
            if (mergeNormalsThreshold > VoxelRenderers.maxFaceCount) mergeNormalsThreshold = VoxelRenderers.maxFaceCount;
            this.meshSize = meshSize;
            this.mergeNormalsThreshold = mergeNormalsThreshold;
            this.jobHorizontalSize = jobHorizontalSize;
            this.seenFromAbove = seenFromAbove;
            this.result = result;
            this.result.Init();
        }


        /// <summary>
        /// Dispose the result
        /// </summary>
        public void Dispose() => result.Dispose();

        /// <summary>
        /// Clear the result
        /// </summary>
        /// <param name="keepOffsets">Whether to keep the lengths for future generations</param>
        public void Clear(bool keepOffsets = false) => result.Clear(keepOffsets);

        /// <summary>
        /// Complete generation and dispose generation jobs
        /// </summary>
        public void Complete() {
            handle.Complete();
            foreach (Generator generator in generators) {
                generator.Dispose();
            }
            generators.Dispose();
            handles.Dispose();
        }


        /// <summary>
        /// Generate meshes from a voxel collection
        /// </summary>
        /// <param name="voxels">The voxels</param>
        /// <param name="offset">Offset to add to the positions</param>
        protected void Generate(VoxelColumns voxels, float3 offset, Generator generator) {
            int nJobsX = (int)math.ceil((float)voxels.sizeX / jobHorizontalSize);
            int nJobsZ = (int)math.ceil((float)voxels.sizeZ / jobHorizontalSize);
            generators = new Unsafe2DArray<Generator>(nJobsX, nJobsZ, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            handles = new(nJobsX, nJobsZ, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            // Create jobs
            for (int jobZ = 0; jobZ < nJobsZ; jobZ++) {
                for (int jobX = 0; jobX < nJobsX; jobX++) {
                    Generator copy = generator;
                    copy.Init();
                    generators[jobX, jobZ] = copy;
                    int jobStartX = jobX * jobHorizontalSize;
                    int jobStartZ = jobZ * jobHorizontalSize;
                    int jobSizeX = math.min(jobHorizontalSize, voxels.sizeX - jobStartX);
                    int jobSizeZ = math.min(jobHorizontalSize, voxels.sizeZ - jobStartZ);
                    GeneratorJob job = new(voxels, jobStartX, jobStartZ, jobSizeX, jobSizeZ, offset, meshSize, mergeNormalsThreshold, seenFromAbove, copy);
                    handles[jobX, jobZ] = job.Schedule();
                }
            }

            ResultJob resultJob = new(result, generators.Array);
            handle = resultJob.Schedule(JobHandle.CombineDependencies(handles.Array));
        }



        [BurstCompile]
        private struct GeneratorJob : IJob {
            private const int chunkSize = 64;

            [ReadOnly] private readonly VoxelColumns voxels; // All voxels
            private readonly int startX, startZ; // Start of the part to generate
            private readonly int sizeX, sizeZ; // Size of the part to generate
            private readonly float3 offset;
            private readonly int meshSize;
            private readonly int mergeNormalsThreshold;
            private readonly bool seenFromAbove;
            private Generator generator; // Final output depending on the type of generator

            private int3 currentMeshStart;
            private int3 currentChunkStart;
            private int3 currentChunkSize;
            private int meshIndex;
            private int startFace;
            private UnsafeArray<ulong> rows;
            private UnsafeArray<bool2> sides;
            private UnsafeArray<ulong> planes;
            private UnsafeHashMap<uint, int> idIndex;
            private UnsafeList<uint> ids;
            private UnsafeList<VoxelFace> faces;
            private UnsafeArray<int2> meshes;

            public GeneratorJob(
                VoxelColumns voxels,
                int startX, int startZ, int sizeX, int sizeZ,
                float3 offset,
                int meshSize, int mergeNormalsThreshold, bool seenFromAbove,
                Generator generator
            ) {
                this.voxels = voxels;
                this.startX = startX;
                this.startZ = startZ;
                this.sizeX = sizeX;
                this.sizeZ = sizeZ;
                this.offset = offset;
                this.meshSize = meshSize;
                this.mergeNormalsThreshold = mergeNormalsThreshold;
                this.seenFromAbove = seenFromAbove;
                this.generator = generator;

                currentMeshStart = 0;
                currentChunkStart = 0;
                currentChunkSize = 0;
                startFace = 0;
                meshIndex = 0;
                rows = default;
                sides = default;
                planes = default;
                idIndex = default;
                ids = default;
                faces = default;
                meshes = default;
            }


            public void Execute() {
                // Find IDs and y ranges
                idIndex = new UnsafeHashMap<uint, int>(0, Allocator.Temp);
                ids = new UnsafeList<uint>(0, Allocator.Temp);
                int nMeshesX = (int)math.ceil((float)sizeX / meshSize);
                int nMeshesZ = (int)math.ceil((float)sizeZ / meshSize);
                Native2DArray<int2> yRanges = new(nMeshesX, nMeshesZ, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                for (int meshZ = 0; meshZ < nMeshesZ; meshZ++) {
                    int meshStartZ = startZ + meshZ * meshSize;
                    int meshEndZ = math.min(meshStartZ + meshSize, startZ + sizeZ);
                    for (int meshX = 0; meshX < nMeshesX; meshX++) {
                        int meshStartX = startX + meshX * meshSize;
                        int meshEndX = math.min(meshStartX + meshSize, startX + sizeX);
                        int min = int.MaxValue, max = int.MinValue;
                        for (int z = meshStartZ; z < meshEndZ; z++) {
                            for (int x = meshStartX; x < meshEndX; x++) {
                                min = math.min(min, voxels.GetMin(x, z));
                                max = math.max(max, voxels.GetMax(x, z));
                                foreach (Voxel voxel in voxels.GetColumn(x, z)) {
                                    uint id = generator.MergeIdentifier(voxel.color);
                                    if (!idIndex.ContainsKey(id)) {
                                        idIndex[id] = ids.Length;
                                        ids.Add(id);
                                    }
                                }
                            }
                        }
                        yRanges[meshX, meshZ] = new(min, max);
                    }
                }

                // Generate all meshes
                rows = new UnsafeArray<ulong>(chunkSize * chunkSize * 3, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                sides = new UnsafeArray<bool2>(chunkSize * chunkSize * 3, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                planes = new UnsafeArray<ulong>(chunkSize * chunkSize * ids.Length * 6, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                faces = new UnsafeList<VoxelFace>(0, Allocator.Temp);
                for (int meshZ = 0; meshZ < nMeshesZ; meshZ++) {
                    currentMeshStart.z = startZ + meshZ * meshSize;
                    int meshEndZ = math.min(currentMeshStart.z + meshSize, startZ + sizeZ);
                    int nChunksZ = (int)math.ceil((float)(meshEndZ - currentMeshStart.z) / chunkSize);
                    for (int meshX = 0; meshX < nMeshesX; meshX++) {
                        currentMeshStart.x = startX + meshX * meshSize;
                        int meshEndX = math.min(currentMeshStart.x + meshSize, startX + sizeX);
                        int nChunksX = (int)math.ceil((float)(meshEndX - currentMeshStart.x) / chunkSize);
                        int2 yRange = yRanges[meshX, meshZ];
                        int nMeshesY = (int)math.ceil((float)(yRange.y - yRange.x) / meshSize);
                        for (int meshY = 0; meshY < nMeshesY; meshY++) {
                            currentMeshStart.y = yRange.x + meshY * meshSize;
                            int meshEndY = math.min(currentMeshStart.y + meshSize, yRange.y);
                            int nChunksY = (int)math.ceil((float)(meshEndY - currentMeshStart.y) / chunkSize);

                            // Generate all chunks
                            meshes = new UnsafeArray<int2>(nChunksX * nChunksY * nChunksZ * 6, Allocator.Temp);
                            meshIndex = 0;
                            for (int chunkZ = 0; chunkZ < nChunksZ; chunkZ++) {
                                currentChunkStart.z = currentMeshStart.z + chunkZ * chunkSize;
                                currentChunkSize.z = math.min(chunkSize, meshEndZ - currentChunkStart.z);
                                for (int chunkX = 0; chunkX < nChunksX; chunkX++) {
                                    currentChunkStart.x = currentMeshStart.x + chunkX * chunkSize;
                                    currentChunkSize.x = math.min(chunkSize, meshEndX - currentChunkStart.x);
                                    for (int chunkY = 0; chunkY < nChunksY; chunkY++) {
                                        currentChunkStart.y = yRange.x + chunkY * chunkSize;
                                        currentChunkSize.y = math.min(chunkSize, yRange.y + 1 - currentChunkStart.y);

                                        // Generate one chunk
                                        rows.Clear();
                                        sides.Clear();
                                        planes.Clear();
                                        GenerateBinarySolidBlocks();
                                        GenerateBinaryPlanes();
                                        GenerateOptimizedMesh();
                                        meshIndex += 6;
                                    }
                                }
                            }

                            GenerateMesh();
                            faces.Clear();
                        }
                    }
                }

                yRanges.Dispose();
                idIndex.Dispose();
                ids.Dispose();
                rows.Dispose();
                sides.Dispose();
                planes.Dispose();
                faces.Dispose();
            }


            // rows: bit rows containing 1 if the block is solid, 0 otherwise
            // sides: 2 bools, first is true if the block before the row is solid, 0 otherwise, second is true if the block after the row is solid, 0 otherwise
            // rows and sides contain chunkSize * chunkSize elements for each axis (x, z, y)
            private void GenerateBinarySolidBlocks() {
                // Rows and y sides
                for (int z = 0; z < currentChunkSize.z; z++) {
                    for (int x = 0; x < currentChunkSize.x; x++) {
                        bool2 ySide = new(false, false);
                        foreach (Voxel voxel in voxels.GetColumn(currentChunkStart.x + x, currentChunkStart.z + z)) {
                            int y = voxel.y - currentChunkStart.y;
                            if (y >= 0 && y < currentChunkSize.y) {
                                rows[y + z * chunkSize] |= 1UL << x; // x
                                rows[x + z * chunkSize + chunkSize * chunkSize] |= 1UL << y; // y
                                rows[y + x * chunkSize + 2 * chunkSize * chunkSize] |= 1UL << z; // z
                            }
                            else if (y == -1) ySide.x = true;
                            else if (y == chunkSize) ySide.y = true;
                        }
                    }
                }

                // x and z sides
                if (currentChunkStart.x > 0) {
                    for (int z = 0; z < currentChunkSize.z; z++) {
                        foreach (Voxel voxel in voxels.GetColumn(currentChunkStart.x - 1, currentChunkStart.z + z)) {
                            int y = voxel.y - currentChunkStart.y;
                            if (y >= 0 && y < currentChunkSize.y) sides[y + z * chunkSize].x = true;
                        }
                    }
                }
                if (currentChunkStart.x + currentChunkSize.x < voxels.sizeX) {
                    for (int z = 0; z < currentChunkSize.z; z++) {
                        foreach (Voxel voxel in voxels.GetColumn(currentChunkStart.x + currentChunkSize.x, currentChunkStart.z + z)) {
                            int y = voxel.y - currentChunkStart.y;
                            if (y >= 0 && y < currentChunkSize.y) sides[y + z * chunkSize].y = true;
                        }
                    }
                }
                if (currentChunkStart.z > 0) {
                    for (int x = 0; x < currentChunkSize.x; x++) {
                        foreach (Voxel voxel in voxels.GetColumn(currentChunkStart.x + x, currentChunkStart.z - 1)) {
                            int y = voxel.y - currentChunkStart.y;
                            if (y >= 0 && y < currentChunkSize.y) sides[y + x * chunkSize + 2 * chunkSize * chunkSize].x = true;
                        }
                    }
                }
                if (currentChunkStart.z + currentChunkSize.z < voxels.sizeZ) {
                    for (int x = 0; x < currentChunkSize.x; x++) {
                        foreach (Voxel voxel in voxels.GetColumn(currentChunkStart.x + x, currentChunkStart.z + currentChunkSize.z)) {
                            int y = voxel.y - currentChunkStart.y;
                            if (y >= 0 && y < currentChunkSize.y) sides[y + x * chunkSize + 2 * chunkSize * chunkSize].y = true;
                        }
                    }
                }
            }


            // planes: 64 bits rows containing 1 if the face must be rendered, 0 otherwise
            // planes contains chunkSize (rows in one plane) * chunkSize (planes in one direction) * nbrIDs * 6 (x+, z+, y+, x-, z-, y-)
            private void GenerateBinaryPlanes() {
                GenerateAxisBinaryPlanes(0);
                GenerateAxisBinaryPlanes(1);
                GenerateAxisBinaryPlanes(2);
            }


            // Generate binary planes for one axis
            private void GenerateAxisBinaryPlanes(int axis) {
                int3 beforeX = currentChunkStart;
                for (int y = 0; y < currentChunkSize[VoxelNormals.HeightAxis(axis)]; y++) {
                    int3 pos = beforeX;
                    for (int x = 0; x < currentChunkSize[VoxelNormals.WidthAxis(axis)]; x++) {
                        ulong row = rows[x + y * chunkSize + axis * chunkSize * chunkSize];
                        bool2 side = sides[x + y * chunkSize + axis * chunkSize * chunkSize];

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
                            uint id = generator.MergeIdentifier(voxels.GetVoxel(posDepth));
                            planes[y + depth * chunkSize + idIndex[id] * chunkSize * chunkSize + 2 * axis * chunkSize * chunkSize * ids.Length] |= 1UL << x;
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
                            uint id = generator.MergeIdentifier(voxels.GetVoxel(posDepth));
                            planes[y + depth * chunkSize + idIndex[id] * chunkSize * chunkSize + (2 * axis + 1) * chunkSize * chunkSize * ids.Length] |= 1UL << x;
                        }

                        pos[VoxelNormals.WidthAxis(axis)]++;
                    }
                    beforeX[VoxelNormals.HeightAxis(axis)]++;
                }
            }


            // Generate the mesh for each plane
            private void GenerateOptimizedMesh() {
                GenerateNormalOptimizedMesh(VoxelNormal.XNegative);
                GenerateNormalOptimizedMesh(VoxelNormal.XPositive);
                GenerateNormalOptimizedMesh(VoxelNormal.YNegative);
                GenerateNormalOptimizedMesh(VoxelNormal.YPositive);
                GenerateNormalOptimizedMesh(VoxelNormal.ZNegative);
                GenerateNormalOptimizedMesh(VoxelNormal.ZPositive);
            }


            // Generate the mesh for a normal
            private void GenerateNormalOptimizedMesh(VoxelNormal normal) {
                int startFace = faces.Length;
                for (int i = 0; i < ids.Length; i++) {
                    for (int depth = 0; depth < currentChunkSize[VoxelNormals.Axis(normal)]; depth++) {
                        GenerateOptimizedPlane(normal, depth, i);
                    }
                }
                meshes[meshIndex + (int)normal] = new int2(startFace, faces.Length);
            }


            // Generate the mesh for a plane and an ID
            private void GenerateOptimizedPlane(VoxelNormal normal, int depth, int index) {
                int startIndex = (int)normal * chunkSize * chunkSize * ids.Length + index * chunkSize * chunkSize + depth * chunkSize;
                int3 beforeX = currentChunkStart;
                beforeX[VoxelNormals.Axis(normal)] += depth;
                for (int y = 0; y < currentChunkSize[VoxelNormals.HeightAxis(normal)]; y++) {
                    int3 pos = beforeX;
                    ulong row = planes[startIndex + y];
                    int x = math.tzcnt(row);
                    pos[VoxelNormals.WidthAxis(normal)] += x;
                    while (x < currentChunkSize[VoxelNormals.WidthAxis(normal)]) {
                        int width = math.tzcnt(~(row >> x)); // Expand in x
                        ulong checkMask = (row << (64 - width - x)) >> (64 - width - x);
                        ulong deleteMask = ~checkMask;

                        int height = 1;
                        while (y + height < currentChunkSize[VoxelNormals.HeightAxis(normal)]) { // Expand in y
                            ref ulong nextRow = ref planes[startIndex + y + height];
                            if ((nextRow & checkMask) != checkMask) break;
                            nextRow &= deleteMask;
                            height++;
                        }

                        faces.Add(new VoxelFace(pos - currentMeshStart, width, height, normal, 0));

                        int prevX = x;
                        x += width;
                        x += math.tzcnt(row >> x);
                        pos[VoxelNormals.WidthAxis(normal)] += x - prevX;
                    }
                    beforeX[VoxelNormals.HeightAxis(normal)]++;
                }
            }


            // Split and merge meshes and generate final output
            private void GenerateMesh() {
                int normalIndex = 0;
                int meshIndex = 0;
                int faceIndex = 0;
                while (normalIndex < 6) {
                    int3 min = int.MaxValue;
                    int3 max = int.MinValue;
                    VoxelNormal meshNormal = VoxelNormal.None;
                    int faceCount = 0;
                    generator.StartMesh(currentMeshStart);

                    // Add all faces of a mesh
                    while (faceCount < VoxelRenderers.maxFaceCount) {
                        // Next mesh
                        if (faceIndex == meshes[meshIndex].y) { // Next mesh
                            meshIndex += 6;
                            if (meshIndex >= meshes.length) { // Next normal
                                normalIndex++;
                                if (normalIndex == 6) break; // End
                                meshIndex = normalIndex;
                                if (faceCount > 0 && faces.Length > mergeNormalsThreshold) { // Generate mesh for this normal
                                    faceIndex = meshes[meshIndex].x;
                                    break;
                                }
                            }
                            faceIndex = meshes[meshIndex].x;
                            continue;
                        }

                        // Add face
                        VoxelFace face = faces[faceIndex];
                        int3 pos = currentMeshStart + new int3(face.X, face.Y, face.Z);
                        int width = face.Width;
                        int height = face.Height;
                        VoxelNormal normal = face.Normal;
                        if (!generator.AddFace(voxels, pos, width, height, normal)) break;
                        if (meshNormal == VoxelNormal.None) meshNormal = normal;
                        else if (meshNormal != normal) meshNormal = VoxelNormal.Any;
                        int3 faceMin = pos;
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
                        generator.AddMesh(offset, offset + (float3)(max + min) / 2f, (float3)(max - min) / 2f, meshNormal, startFace, faceCount);
                        startFace += faceCount;
                    }
                }
            }
        }
    
    

        [BurstCompile]
        private readonly struct ResultJob : IJob {
            private readonly Result result;
            [ReadOnly] private readonly UnsafeArray<Generator> generators;

            public ResultJob(Result result, UnsafeArray<Generator> generators) {
                this.result = result;
                this.generators = generators;
            }

            public readonly void Execute() => result.Add(generators);
        }
    }



    /// <summary>
    /// Implementation of a specific generator
    /// </summary>
    internal interface IMeshGenerator {
        void Init();
        void Dispose();
        
        /// <summary>
        /// Get an identifier for a voxel.
        /// Faces with the same identifier can be merged.
        /// </summary>
        uint MergeIdentifier(Color32 voxel);

        /// <summary>
        /// Start generating a mesh
        /// </summary>
        /// <param name="meshStart">Start position of the mesh</param>
        void StartMesh(int3 meshStart);

        /// <summary>
        /// Add a face to the mesh
        /// </summary>
        /// <returns>Whether the face can be added in the current mesh</returns>
        bool AddFace(VoxelColumns voxels, int3 pos, int width, int height, VoxelNormal normal);

        /// <summary>
        /// Stop generating the mesh and add it
        /// </summary>
        void AddMesh(float3 offset, float3 center, float3 size, VoxelNormal normal, int startFace, int faceCount);
    }


    internal interface IMeshResult<Generator> where Generator : unmanaged, IMeshGenerator {
        void Init();
        void Dispose();
        void Clear(bool keepOffsets);

        /// <summary>
        /// Add the result of multiple generators
        /// </summary>
        void Add(UnsafeArray<Generator> generators);
    }



    internal class TerrainMeshGenerator : MeshGenerator<TerrainMeshGenerator.Generator, TerrainMeshGenerator.Result> {
        public TerrainMeshGenerator(int meshSize = 64, int mergeNormalsThreshold = 256, int jobHorizontalSize = int.MaxValue) :
            base(default, meshSize, mergeNormalsThreshold, jobHorizontalSize, true) {}
        public void Generate(VoxelColumns voxels, float3 offset) => Generate(voxels, offset, default);


        internal struct Data {
            public NativeList<VoxelFace> faces;
            public NativeList<VoxelMesh> meshes;
            public NativeList<Color32> colors;
            public NativeHashMap<uint, int> colorIndices;

            public void Init() {
                faces = new(Allocator.Persistent);
                meshes = new(Allocator.Persistent);
                colors = new(Allocator.Persistent);
                colorIndices = new(0, Allocator.Persistent);
            }

            public void Dispose() {
                faces.Dispose();
                meshes.Dispose();
                colors.Dispose();
                colorIndices.Dispose();
            }
        }


        internal struct Generator : IMeshGenerator {
            [NativeDisableContainerSafetyRestriction] public Data data;
            private int3 meshStart;

            public void Init() => data.Init();
            public void Dispose() => data.Dispose();
            public readonly uint MergeIdentifier(Color32 voxel) => Identifiers.ColorToId(voxel);
            public void StartMesh(int3 meshStart) => this.meshStart = meshStart;

            public bool AddFace(VoxelColumns voxels, int3 pos, int width, int height, VoxelNormal normal) {
                Color32 color = voxels.GetVoxel(pos);
                uint id = Identifiers.ColorToId(color);
                if (!data.colorIndices.TryGetValue(id, out int index)) {
                    index = data.colors.Length;
                    data.colorIndices[id] = index;
                    data.colors.Add(color);
                }
                data.faces.Add(new VoxelFace(pos - meshStart, width, height, normal, index));
                return true;
            }

            public void AddMesh(float3 offset, float3 center, float3 size, VoxelNormal normal, int startFace, int faceCount)
                => data.meshes.Add(new VoxelMesh(center, size, offset + meshStart, normal, faceCount, startFace));
        }


        internal struct Result : IMeshResult<Generator> {
            public Data data;
            private int startFace;

            public void Init() => data.Init();
            public void Dispose() => data.Dispose();

            public void Clear(bool keepOffsets) {
                if (!keepOffsets) {
                    startFace = 0;
                    data.colorIndices.Clear();
                }
                data.faces.Clear();
                data.meshes.Clear();
                data.colors.Clear();
            }

            public void Add(UnsafeArray<Generator> generators) {
                // Increase capacity
                int addedFaces = 0;
                int addedMeshes = 0;
                foreach (Generator generator in generators) {
                    addedFaces += generator.data.faces.Length;
                    addedMeshes += generator.data.meshes.Length;
                }
                data.faces.Capacity = data.faces.Length + addedFaces;
                data.meshes.Capacity = data.meshes.Length + addedMeshes;

                foreach (Generator generator in generators) {
                    // Merge colors
                    UnsafeArray<int> colorMap = new(generator.data.colors.Length, Allocator.Temp);
                    foreach (KVPair<uint, int> kv in generator.data.colorIndices) {
                        if (data.colorIndices.TryGetValue(kv.Key, out int index)) {
                            colorMap[kv.Value] = index;
                        }
                        else {
                            colorMap[kv.Value] = data.colorIndices.Count;
                            data.colorIndices[kv.Key] = data.colorIndices.Count;
                            data.colors.Add(Identifiers.IdToColor(kv.Key));
                        }
                    }
                    if (data.colorIndices.Count > VoxelFace.maxColor) throw new InvalidOperationException($"Number of colors can't exceed {VoxelFace.maxColor}");

                    // Modify and add faces and meshes
                    foreach (VoxelFace face in generator.data.faces) {
                        data.faces.Add(new VoxelFace(
                            new(face.X, face.Y, face.Z), face.Width, face.Height, face.Normal,
                            colorMap[face.Color]
                        ));
                    }
                    foreach (VoxelMesh mesh in generator.data.meshes) {
                        data.meshes.Add(new VoxelMesh(
                            mesh.center, mesh.size, mesh.position, mesh.Normal, mesh.FaceCount,
                            mesh.StartFace + startFace
                        ));
                    }
                    startFace = data.faces.Length;
                }
            }
        }


        public NativeArray<VoxelFace> Faces => result.data.faces.AsArray();
        public NativeArray<VoxelMesh> Meshes => result.data.meshes.AsArray();
        public NativeArray<Color32> Colors => result.data.colors.AsArray();
    }



    internal class ObjectMeshGenerator : MeshGenerator<ObjectMeshGenerator.Generator, ObjectMeshGenerator.Result> {
        public ObjectMeshGenerator(int meshSize = 64, int mergeNormalsThreshold = 256, int jobHorizontalSize = int.MaxValue) :
            base(default, meshSize, mergeNormalsThreshold, jobHorizontalSize, false) {}

        public void Generate(VoxelColumns voxels, float3 offset, int startInstance)
            => Generate(voxels, offset, new Generator() { startInstance = startInstance });


        internal struct Data {
            public NativeList<VoxelFace> faces;
            public NativeList<ObjectMesh> meshes;
            public NativeList<Color32> colors;

            public void Init() {
                faces = new(Allocator.Persistent);
                meshes = new(Allocator.Persistent);
                colors = new(Allocator.Persistent);
            }

            public void Dispose() {
                faces.Dispose();
                meshes.Dispose();
                colors.Dispose();
            }
        }


        internal struct Generator : IMeshGenerator {
            [NativeDisableContainerSafetyRestriction] public Data data;
            private int3 meshStart;
            private int startColor;
            public int startInstance;

            public void Init() => data.Init();
            public void Dispose() => data.Dispose();
            public readonly uint MergeIdentifier(Color32 voxel) => 0;

            public void StartMesh(int3 meshStart) {
                this.meshStart = meshStart;
                startColor = data.colors.Length;
            }

            public bool AddFace(VoxelColumns voxels, int3 pos, int width, int height, VoxelNormal normal) {
                if (data.colors.Length - startColor + width * height - 1 > VoxelFace.maxColor) return false;
                data.faces.Add(new VoxelFace(pos - meshStart, width, height, normal, data.colors.Length - startColor));
                for (int x = 0; x < width; x++) {
                    for (int y = 0; y < height; y++) {
                        int3 texturePos = pos;
                        texturePos[VoxelNormals.WidthAxis(normal)] += x;
                        texturePos[VoxelNormals.HeightAxis(normal)] += y;
                        data.colors.Add(voxels.GetVoxel(texturePos));
                    }
                }
                return true;
            }

            public void AddMesh(float3 offset, float3 center, float3 size, VoxelNormal normal, int startFace, int faceCount)
                => data.meshes.Add(new ObjectMesh(center, size, offset + meshStart, normal, faceCount, startFace, startColor, startInstance));
        }


        internal struct Result : IMeshResult<Generator> {
            public Data data;
            private int startFace;
            private int startColor;

            public void Init() => data.Init();
            public void Dispose() => data.Dispose();

            public void Clear(bool keepOffsets) {
                if (!keepOffsets) {
                    startFace = 0;
                    startColor = 0;
                }
                data.faces.Clear();
                data.meshes.Clear();
                data.colors.Clear();
            }

            public void Add(UnsafeArray<Generator> generators) {
                // Increase capacity
                int addedFaces = 0;
                int addedMeshes = 0;
                int addedColors = 0;
                foreach (Generator generator in generators) {
                    addedFaces += generator.data.faces.Length;
                    addedMeshes += generator.data.meshes.Length;
                    addedColors += generator.data.colors.Length;
                }
                data.faces.Capacity = data.faces.Length + addedFaces;
                data.meshes.Capacity = data.meshes.Length + addedMeshes;
                data.colors.Capacity = data.meshes.Length + addedColors;

                // Add faces and colors and add and modify meshes
                foreach (Generator generator in generators) {
                    data.faces.AddRange(generator.data.faces.AsArray());
                    data.colors.AddRange(generator.data.colors.AsArray());
                    foreach (ObjectMesh mesh in generator.data.meshes) {
                        data.meshes.Add(new ObjectMesh(
                            mesh.mesh.center, mesh.mesh.size, mesh.mesh.position, mesh.mesh.Normal, mesh.mesh.FaceCount,
                            mesh.mesh.StartFace + startFace, mesh.StartColor + startColor, mesh.StartInstance
                        ));
                    }
                    startFace = data.faces.Length;
                    startColor = data.colors.Length;
                }
            }
        }


        public NativeArray<VoxelFace> Faces => result.data.faces.AsArray();
        public NativeArray<ObjectMesh> Meshes => result.data.meshes.AsArray();
        public NativeArray<Color32> Colors => result.data.colors.AsArray();
    }



    /// <summary>
    /// Convert data to and from identifiers
    /// </summary>
    internal static class Identifiers {
        public static uint ColorToId(Color32 color) => color.r | (uint)color.g << 8 | (uint)color.b << 16 | (uint)color.a << 24;
        public static Color32 IdToColor(uint id) => new((byte)(id & 0xFF), (byte)(id >> 8 & 0xFF), (byte)(id >> 16 & 0xFF), (byte)(id >> 24 & 0xFF));
    }

}