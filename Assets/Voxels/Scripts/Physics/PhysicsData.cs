using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace Voxels.Physics {
    
    /// <summary>
    /// Burst-compatible global physics data
    /// </summary>
    [BurstCompile]
    internal struct PhysicsData {
        private NativeList<LinkedCollider> colliders; // Linked lists of colliders
        private NativeList<int> octree; // 9 ints per node pointing to the index of its first collider and its children
        private int root; // Octree root
        private int reusableCollider; // Index of the first reusable collider in [colliders]
        private int reusableNode; // Index of the first reusable node in [octree]
        private readonly int maxDepth; // Max depth of the octree
        private readonly float3 offset; // Offset of the octree
        private readonly float size; // Size of the octree

        // Specific data for each collider type
        private NativeList<BinaryOctree> meshColliders;
        private NativeList<Box> boxColliders;


        public PhysicsData(int maxDepth, float3 offset, float size) {
            colliders = new(Allocator.Persistent);
            octree = new(Allocator.Persistent);
            meshColliders = new(Allocator.Persistent);
            boxColliders = new(Allocator.Persistent);
            root = -1;
            reusableCollider = -1;
            reusableNode = -1;
            this.maxDepth = maxDepth;
            this.offset = offset;
            this.size = size;
        }

        public void Dispose() {
            colliders.Dispose();
            octree.Dispose();
            meshColliders.Dispose();
            boxColliders.Dispose();
        }


        /// <summary>
        /// Add a mesh collider
        /// </summary>
        /// <param name="octree">Octree representing the collider</param>
        /// <param name="layer">Layer of the collider</param>
        /// <returns>Index of the collider</returns>
        public int AddMeshCollider(BinaryOctree octree, int layer) {
            int index = AddCollider(octree.bounds);
            colliders[index] = new LinkedCollider(ColliderType.Mesh, meshColliders.Length, 1 << layer, colliders[index].next);
            meshColliders.Add(octree);
            return index;
        }

        /// <summary>
        /// Add a box collider
        /// </summary>
        /// <param name="box">The box</param>
        /// <param name="layer">Layer of the collider</param>
        /// <returns>Index of the collider</returns>
        public int AddBoxCollider(Box box, int layer) {
            int index = AddCollider(box);
            colliders[index] = new LinkedCollider(ColliderType.Box, boxColliders.Length, 1 << layer, colliders[index].next);
            boxColliders.Add(box);
            return index;
        }

        private int AddCollider(Box bounds) {
            if (math.any(bounds.min < offset) || math.any(bounds.max > offset + size))
                throw new ArgumentOutOfRangeException($"Collider with bounds {bounds} is out of range of physics octree");
            int index;
            if (reusableCollider == -1) {
                index = colliders.Length++;
            }
            else {
                index = reusableCollider;
                reusableCollider = colliders[index].next;
            }
            root = AddCollider(root, size / 2, maxDepth, index, bounds - offset);
            return index;
        }

        private int AddCollider(int node, float childSize, int maxDepth, int index, Box bounds) {
            if (node == -1) { // Create new node
                if (reusableNode == -1) {
                    node = octree.Length / 9;
                    octree.Length += 9;
                }
                else {
                    node = reusableNode;
                    reusableNode = octree[9 * reusableNode];
                }
                for (int i = 0; i < 9; i++) {
                    octree[9 * node + i] = -1;
                }
            }

            bool3 minAfterCenter = bounds.min > childSize;
            bool3 maxBeforeCenter = bounds.max < childSize;
            if (maxDepth > 0 && math.all(minAfterCenter | maxBeforeCenter)) { // The collider fits in a child
                int childNode = 9 * node + 1 + math.bitmask(new bool4(minAfterCenter, false));
                Box childBounds = bounds - math.select(0, childSize, minAfterCenter);
                octree[childNode] = AddCollider(octree[childNode], childSize / 2, maxDepth - 1, index, childBounds);
            }
            else { // Add in this node
                colliders[index] = new LinkedCollider(default, 0, 0, octree[9 * node]);
                octree[9 * node] = index;
            }
            return node;
        }


        /// <summary>
        /// Remove a mesh collider
        /// </summary>
        /// <param name="index">Index of the collider</param>
        /// <param name="swapIndex">Index of the last mesh collider</param>
        public void RemoveMeshCollider(int index, int swapIndex) {
            int meshIndex = colliders[index].index;
            root = RemoveCollider(root, size / 2, maxDepth, index, meshColliders[meshIndex].bounds);
            meshColliders.RemoveAtSwapBack(meshIndex);
            if (meshIndex < meshColliders.Length)
                colliders[swapIndex] = new LinkedCollider(ColliderType.Mesh, meshIndex, colliders[swapIndex].layerMask, colliders[swapIndex].next);
        }

        /// <summary>
        /// Remove a box collider
        /// </summary>
        /// <param name="index">Index of the collider in the collider array</param>
        /// <param name="swapIndex">Index of the last box collider</param>
        public void RemoveBoxCollider(int index, int swapIndex) {
            int boxIndex = colliders[index].index;
            root = RemoveCollider(root, size / 2, maxDepth, index, boxColliders[boxIndex]);
            boxColliders.RemoveAtSwapBack(boxIndex);
            if (boxIndex < boxColliders.Length)
                colliders[swapIndex] = new LinkedCollider(ColliderType.Box, boxIndex, colliders[swapIndex].layerMask, colliders[swapIndex].next);
        }

        private int RemoveCollider(int node, float childSize, int maxDepth, int index, Box bounds) {
            return 0;
        }


        /// <summary>
        /// Raycast query
        /// </summary>
        /// <param name="origin">Origin of the ray</param>
        /// <param name="direction">Direction of the ray</param>
        /// <param name="maxDistance">Maximum distance between the origin and the hit point</param>
        /// <param name="layerMask">Layers of colliders that are considered</param>
        /// <param name="point">Hit point</param>
        /// <param name="normal">Normal of the face that was hit</param>
        /// <param name="type">Type of the collider that was hit</param>
        /// <param name="index">Index of the collider that was hit</param>
        /// <returns>Whether the ray hit a collider</returns>
        public bool Raycast(
            float3 origin, float3 direction, float maxDistance, int layerMask,
            out float3 point, out float3 normal, out ColliderType type, out int index
        ) {
            if (math.any(origin < offset) || math.any(origin > offset + size))
                throw new ArgumentOutOfRangeException($"Ray origin {origin} is out of range of physics octree");
            bool hit = Raycast(root, offset + size / 2, size / 2, origin, direction, maxDistance, layerMask, out float hitDistance, out int hitAxis, out int hitIndex);
            if (hit) {
                point = origin + direction * hitDistance;
                normal = GetNormal(direction, hitAxis);
                type = colliders[hitIndex].type;
                index = colliders[hitIndex].index;
            }
            else {
                point = 0;
                normal = 0;
                type = ColliderType.None;
                index = -1;
            }
            return hit;
        }

        private bool Raycast(
            int node, float3 center, float childSize,
            float3 origin, float3 direction, float maxDistance, int layerMask,
            out float hitDistance, out int hitAxis, out int hitIndex
        ) {
            hitAxis = -1;
            hitIndex = -1;
            if (node == -1) { // No hit in this node
                hitDistance = float.PositiveInfinity;
                return false;
            }
            
            // Raycast in all colliders in this node
            for (int i = octree[9 * node]; i != -1; i = colliders[i].next) {
                if ((colliders[i].layerMask & layerMask) == 0) continue;
                int axis = 0;
                float distance = 0;
                bool hit = colliders[i].type switch {
                    ColliderType.Mesh => meshColliders[colliders[i].index].Raycast(origin, direction, maxDistance, out distance, out axis),
                    ColliderType.Box => boxColliders[colliders[i].index].Raycast(origin, direction, maxDistance, out distance, out axis),
                    _ => false
                };
                if (hit) {
                    maxDistance = distance;
                    hitAxis = axis;
                    hitIndex = i;
                }
            }

            // Raycast in children traversed by the ray
            float3 distances = (center - origin) / direction;
            bool3 side = origin > center;
            float addedDistance = 0;
            float3 childOrigin = origin;
            for (int i = 0; i < 4; i++) {
                // Raycast in child
                int childNode = octree[9 * node + 1 + math.bitmask(new bool4(side, false))];
                float halfChildSize = childSize / 2;
                float3 childCenter = math.select(center - halfChildSize, center + halfChildSize, side);
                if (Raycast(childNode, childCenter, halfChildSize, childOrigin, direction, maxDistance - addedDistance, layerMask, out float distance, out int axis, out int index)) {
                    maxDistance = distance + addedDistance;
                    hitAxis = axis;
                    hitIndex = index;
                    break;
                }

                // Find next child
                axis = -1;
                addedDistance = maxDistance;
                for (int j = 0; j < 3; j++) {
                    float d = distances[j];
                    float3 intersection = origin + d * direction;
                    intersection[j] = center[j];
                    if (d >= 0 && d < addedDistance && math.all(intersection >= center - childSize & intersection <= center + childSize)) {
                        addedDistance = d;
                        axis = j;
                        childOrigin = intersection;
                    }
                }
                if (axis == -1) break;
                side[axis] = !side[axis];
                distances[axis] = float.PositiveInfinity;
            }

            if (hitAxis == -1) { // No hit in this node
                hitDistance = float.PositiveInfinity;
                return false;
            }
            else { // Some hits in this node
                hitDistance = maxDistance;
                return true;   
            }
        }

        private static float3 GetNormal(float3 direction, int axis) {
            float3 normal = 0;
            normal[axis] = -math.sign(direction[axis]);
            return normal;
        }
        
        
        [BurstCompile]
        public static int AddMeshCollider(ref PhysicsData @this, in BinaryOctree octree, int layer)
            => @this.AddMeshCollider(octree, layer);

        [BurstCompile]
        public static int AddBoxCollider(ref PhysicsData @this, in Box box, int layer)
            => @this.AddBoxCollider(box, layer);

        [BurstCompile]
        public static void RemoveMeshCollider(ref PhysicsData @this, int index, int swapIndex)
            => @this.RemoveMeshCollider(index, swapIndex);

        [BurstCompile]
        public static void RemoveBoxCollider(ref PhysicsData @this, int index, int swapIndex)
            => @this.RemoveBoxCollider(index, swapIndex);

        [BurstCompile]
        public static bool Raycast(
            ref PhysicsData @this, in float3 origin, in float3 direction, float maxDistance, int layerMask,
            out float3 point, out float3 normal, out ColliderType type, out int index
        ) => @this.Raycast(origin, direction, maxDistance, layerMask, out point, out normal, out type, out index);


        private readonly struct LinkedCollider {
            public readonly ColliderType type;
            public readonly int index;
            public readonly int layerMask;
            public readonly int next;

            public LinkedCollider(ColliderType type, int index, int layerMask, int next) {
                this.type = type;
                this.index = index;
                this.layerMask = layerMask;
                this.next = next;
            }
        }
    }


    internal enum ColliderType {
        Mesh,
        Box,
        None
    }

}