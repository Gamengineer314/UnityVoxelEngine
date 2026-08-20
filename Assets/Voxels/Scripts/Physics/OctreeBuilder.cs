using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using Voxels.Collections;

namespace Voxels.Physics {
    
    /// <summary>
    /// Octree of voxels
    /// </summary>
    [BurstCompile]
    internal struct OctreeBuilder {
        public const int empty = -1;
        public const int full = -2;

        public NativeList<int> children; // 8 ints per node pointing to its children
        public readonly int size;
        public int root;
        private int reusable;


        public OctreeBuilder(VoxelColumns voxels) {
            children = new NativeList<int>(Allocator.Persistent);
            size = math.ceilpow2(math.max(voxels.size.x, math.max(voxels.size.y, voxels.size.z)));
            root = -1;
            reusable = -1;
        }

        public void Dispose() => children.Dispose();


        [BurstCompile]
        public static void Add(ref OctreeBuilder octree, in OctreeGeneratorJob job) {
            int start = octree.children.Length / 8;
            foreach (int child in job.children) {
                octree.children.Add(child == full || child == empty ? child : child + start);
            }
            for (int octantY = 0; octantY < job.roots.Length; octantY++) {
                int3 coords = new(job.startX, octantY * job.size, job.startZ);
                int root = job.roots[octantY];
                if (root == empty) continue;
                if (root != full) root += start;
                octree.root = Set(octree.root, root, job.size, coords, octree.size, octree.children, ref octree.reusable);
            }
        }

        public static int Set(int node, int value, int valueSize, int3 coords, int size, NativeList<int> children, ref int reusable) {
            if (size == valueSize) return value;
            
            if (node == empty) { // Add node
                if (reusable == -1) {
                    node = children.Length / 8;
                    children.Length += 8;
                }
                else {
                    node = reusable;
                    reusable = children[8 * reusable];
                }
                for (int i = 0; i < 8; i++) {
                    children[8 * node + i] = empty;
                }
            }

            // Add in child
            int childSize = size / 2;
            bool3 side = coords >= childSize;
            int childNode = 8 * node + math.bitmask(new bool4(side, false));
            int3 childCoords = math.select(coords, coords - childSize, side);
            children[childNode] = Set(children[childNode], value, valueSize, childCoords, childSize, children, ref reusable);

            // Remove node if all children are full
            for (int i = 0; i < 8; i++) {
                if (children[8 * node + i] != full) return node;
            }
            children[8 * node] = reusable;
            reusable = node;
            return full;
        }

    }

}