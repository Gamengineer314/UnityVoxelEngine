using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Voxels.Collections;

namespace Voxels.Physics {

    internal class OctreeGenerator : ParallelGenerator<VoxelColumns, OctreeGeneratorJob> {
        private readonly Dictionary<VoxelColumns, OctreeBuilder> octrees;

        /// <summary>
        /// Create an octree generator
        /// </summary>
        /// <param name="octrees">Octree builders where the results will be added</param>
        public OctreeGenerator(Dictionary<VoxelColumns, OctreeBuilder> octrees) {
            this.octrees = octrees;
        }

        protected override IEnumerable<OctreeGeneratorJob> CreateJobs(VoxelColumns command, int jobHorizontalSize) {
            if (jobHorizontalSize <= 0 || (jobHorizontalSize & jobHorizontalSize - 1) != 0)
                throw new ArgumentException($"Job size must be a power of 2", nameof(jobHorizontalSize));
            if (octrees.ContainsKey(command)) return null;

            OctreeBuilder octree = new(command);
            octrees[command] = octree;
            jobHorizontalSize = math.min(jobHorizontalSize, octree.size);

            List<OctreeGeneratorJob> jobs = new();
            int nJobsX = (int)math.ceil((float)command.size.x / jobHorizontalSize);
            int nJobsZ = (int)math.ceil((float)command.size.z / jobHorizontalSize);
            for (int jobZ = 0, i = 0; jobZ < nJobsZ; jobZ++) {
                for (int jobX = 0; jobX < nJobsX; jobX++, i++) {
                    int jobStartX = jobX * jobHorizontalSize;
                    int jobStartZ = jobZ * jobHorizontalSize;
                    jobs.Add(new OctreeGeneratorJob(command, jobStartX, jobStartZ, jobHorizontalSize));
                }
            }
            return jobs;
        }

        protected override void ProcessResult(VoxelColumns command, OctreeGeneratorJob job) {
            OctreeBuilder octree = octrees[command];
            OctreeBuilder.Add(ref octree, job);
            octrees[command] = octree;
        }
    }



    [BurstCompile]
    internal struct OctreeGeneratorJob : IJob, IDisposable {
        [ReadOnly] public readonly VoxelColumns voxels; // All voxels
        public readonly int startX, startZ; // Start of the part to generate
        public readonly int size; // Size of the octants to generate
        public NativeArray<int> roots; // Roots for each octant in the generated column
        public NativeList<int> children; // 8 ints per node pointing to its children
        private int reusable;

        public OctreeGeneratorJob(VoxelColumns voxels, int startX, int startZ, int size) {
            this.voxels = voxels;
            this.startX = startX;
            this.startZ = startZ;
            this.size = size;
            roots = new((int)math.ceil((float)voxels.size.y / size), Allocator.Persistent);
            for (int i = 0; i < roots.Length; i++) {
                roots[i] = -1;
            }
            children = new(Allocator.Persistent);
            reusable = -1;
        }

        public void Dispose() {
            roots.Dispose();
            children.Dispose();
        }

        public void Execute() {
            int endX = math.min(startX + size, voxels.size.x);
            int endZ = math.min(startZ + size, voxels.size.z);
            for (int x = startX; x < endX; x++) {
                for (int z = startZ; z < endZ; z++) {
                    foreach (Voxel voxel in voxels.GetColumn(x, z)) {
                        if (Voxel.Color32Equals(voxel.color, Voxel.ghost)) continue;
                        int octantY = voxel.y / size;
                        int3 coords = new(x - startX, voxel.y % size, z - startZ);
                        roots[octantY] = OctreeBuilder.Set(roots[octantY], OctreeBuilder.full, 1, coords, size, children, ref reusable);
                    }
                }
            }
        }
    }

}