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
        public NativeList<LinkedCollider> colliders; // Linked lists of colliders
        private NativeList<int> octree; // 9 ints per node pointing to the index of its first collider and its children
        private int root; // Octree root
        private int reusableCollider; // Index of the first reusable collider in [colliders]
        private int reusableNode; // Index of the first reusable node in [octree]
        private readonly int maxDepth; // Max depth of the octree
        private readonly float3 offset; // Offset of the octree
        private readonly float size; // Size of the octree

        // Specific data for each collider type
        public NativeList<TransformedOctree> meshColliders;
        public NativeList<Box> boxColliders;


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
        /// <returns>Index of the collider in the collider array</returns>
        private int AddMeshCollider(TransformedOctree octree, int layer) {
            int index = AddCollider(octree.bounds);
            colliders[index] = new LinkedCollider(ColliderType.Mesh, meshColliders.Length, layer, colliders[index].next);
            meshColliders.Add(octree);
            return index;
        }

        /// <summary>
        /// Add a box collider
        /// </summary>
        /// <param name="box">The box</param>
        /// <param name="layer">Layer of the collider</param>
        /// <returns>Index of the collider in the collider array</returns>
        private int AddBoxCollider(Box box, int layer) {
            int index = AddCollider(box);
            colliders[index] = new LinkedCollider(ColliderType.Box, boxColliders.Length, layer, colliders[index].next);
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
            root = AddCollider(root, size, maxDepth, index, bounds - offset);
            return index;
        }

        private int AddCollider(int node, float size, int maxDepth, int index, Box bounds) {
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

            float childSize = size / 2;
            bool3 minAfterCenter = bounds.min > childSize;
            bool3 maxBeforeCenter = bounds.max < childSize;
            if (maxDepth > 0 && math.all(minAfterCenter | maxBeforeCenter)) { // The collider fits in a child
                int childNode = 9 * node + 1 + math.bitmask(new bool4(minAfterCenter, false));
                Box childBounds = bounds - math.select(0, childSize, minAfterCenter);
                octree[childNode] = AddCollider(octree[childNode], childSize, maxDepth - 1, index, childBounds);
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
        /// <param name="index">Index of the collider in the collider array</param>
        /// <param name="swapIndex">Index of the last mesh collider</param>
        /// <returns>Index of the collider in the mesh collider array</returns>
        private int RemoveMeshCollider(int index, int swapIndex) {
            int meshIndex = colliders[index].index;
            root = RemoveCollider(root, size, index, meshColliders[meshIndex].bounds - offset);
            meshColliders.RemoveAtSwapBack(meshIndex);
            if (meshIndex < meshColliders.Length)
                colliders[swapIndex] = new LinkedCollider(ColliderType.Mesh, meshIndex, colliders[swapIndex].layer, colliders[swapIndex].next);
            return meshIndex;
        }

        /// <summary>
        /// Remove a box collider
        /// </summary>
        /// <param name="index">Index of the collider in the collider array</param>
        /// <param name="swapIndex">Index of the last box collider</param>
        /// <returns>Index of the collider in the box collider array</returns>
        private int RemoveBoxCollider(int index, int swapIndex) {
            int boxIndex = colliders[index].index;
            root = RemoveCollider(root, size, index, boxColliders[boxIndex] - offset);
            boxColliders.RemoveAtSwapBack(boxIndex);
            if (boxIndex < boxColliders.Length)
                colliders[swapIndex] = new LinkedCollider(ColliderType.Box, boxIndex, colliders[swapIndex].layer, colliders[swapIndex].next);
            return boxIndex;
        }

        private int RemoveCollider(int node, float size, int index, Box bounds) {
            float childSize = size / 2;
            bool3 minAfterCenter = bounds.min > childSize;
            bool3 maxBeforeCenter = bounds.max < childSize;
            if (maxDepth > 0 && math.all(minAfterCenter | maxBeforeCenter)) { // The collider fits in a child
                int childNode = 9 * node + 1 + math.bitmask(new bool4(minAfterCenter, false));
                Box childBounds = bounds - math.select(0, childSize, minAfterCenter);
                octree[childNode] = RemoveCollider(octree[childNode], childSize, index, childBounds);
            }
            else { // Remove in this node
                int i = octree[9 * node];
                if (i == index) {
                    octree[9 * node] = colliders[index].next;
                }
                else {
                    while (colliders[i].next != index) {
                        i = colliders[i].next;
                    }
                    colliders[i] = new LinkedCollider(colliders[i].type, colliders[i].index, colliders[i].layer, colliders[index].next);
                }
                colliders[index] = new LinkedCollider(default, 0, 0, reusableCollider);
                reusableCollider = index;
            }

            // Remove node if empty
            for (int i = 0; i < 9; i++) {
                if (octree[9 * node + i] != -1) return node;
            }
            octree[9 * node] = reusableNode;
            reusableNode = node;
            return -1;
        }


        /// <summary>
        /// Raycast query
        /// </summary>
        /// <param name="origin">Origin of the ray</param>
        /// <param name="direction">Direction of the ray</param>
        /// <param name="distance">Maximum distance between the origin and the hit point</param>
        /// <param name="layerMask">Layers of colliders that are considered</param>
        /// <param name="hitInfo">Information about the hit point if the ray hit a collider</param>
        /// <returns>Whether the ray hit a collider</returns>
        private bool Raycast(float3 origin, float3 direction, float distance, int layerMask, out RaycastHit hitInfo) {
            if (math.any(origin < offset) || math.any(origin > offset + size))
                throw new ArgumentOutOfRangeException($"Ray origin {origin} is out of range of physics octree");
            bool hit = Raycast(root, offset, size, origin, direction, 1 / direction, layerMask, ref distance, out int axis, out int index);
            hitInfo = GetInfo(hit, direction, distance, axis, index);
            return hit;
        }

        private bool Raycast(
            int node, float3 start, float size,
            float3 origin, float3 direction, float3 inverse, int layerMask,
            ref float distance, out int hitAxis, out int hitIndex
        ) {
            hitAxis = -1;
            hitIndex = -1;
            if (node == -1) return false;
            
            // Raycast in all colliders in this node
            for (int i = octree[9 * node]; i != -1; i = colliders[i].next) {
                if ((layerMask & 1 << colliders[i].layer) == 0) continue;
                int axis = 0;
                bool hit = colliders[i].type switch {
                    ColliderType.Mesh => meshColliders[colliders[i].index].Raycast(origin, direction, inverse, ref distance, out axis),
                    ColliderType.Box => boxColliders[colliders[i].index].Raycast(origin, inverse, ref distance, out axis),
                    _ => false
                };
                if (hit) {
                    hitAxis = axis;
                    hitIndex = i;
                }
            }

            // Find exit distance for this node
            float3 exitPlanes = math.select(0, size, inverse > 0);
            float3 exitDistances = (exitPlanes - origin) * inverse;
            float minExitDistance = distance;
            for (int i = 0; i < 3; i++) {
                if (exitDistances[i] < minExitDistance) {
                    minExitDistance = exitDistances[i];
                }
            }

            // Raycast in children traversed by the ray
            float childSize = size / 2;
            float3 center = start + childSize;
            bool3 side = origin > center;
            float3 distances = (center - origin) * inverse;
            distances = math.select(float.PositiveInfinity, distances, distances >= 0);
            float addedDistance = 0;
            for (int i = 0; i < 4; i++) {
                // Raycast in child
                int childNode = octree[9 * node + 1 + math.bitmask(new bool4(side, false))];
                float3 childStart = math.select(start, start + childSize, side);
                float childDistance = distance - addedDistance;
                float3 childOrigin = origin + direction * addedDistance;
                if (Raycast(childNode, childStart, childSize, childOrigin, direction, inverse, layerMask, ref childDistance, out int axis, out int index)) {
                    distance = childDistance + addedDistance;
                    hitAxis = axis;
                    hitIndex = index;
                    return true;
                }

                // Find next child
                axis = 0;
                addedDistance = distances[0];
                for (int j = 1; j < 3; j++) {
                    if (distances[j] < addedDistance) {
                        axis = j;
                        addedDistance = distances[j];
                    }
                }
                if (addedDistance > minExitDistance) break;
                side[axis] = !side[axis];
                distances[axis] = float.PositiveInfinity;
            }

            return hitAxis != -1;
        }


        /// <summary>
        /// Move query with a box shape
        /// </summary>
        /// <param name="origin">Start position of the box</param>
        /// <param name="direction">Direction of the box</param>
        /// <param name="distance">Maximum distance between the origin and the hit point</param>
        /// <param name="layerMask">Layers of colliders that are considered</param>
        /// <param name="hitInfo">Information about the hit point if the box hit a collider</param>
        /// <returns>Whether the box hit a voxel</returns>
        private bool MoveBox(Box origin, float3 direction, float distance, int layerMask, out RaycastHit hitInfo) {
            if (math.any(origin.min < offset) || math.any(origin.max > offset + size))
                throw new ArgumentOutOfRangeException($"Move origin {origin} is out of range of physics octree");
            bool hit = MoveBox(root, offset, size, origin, direction, 1 / direction, layerMask, ref distance, out int axis, out int index);
            hitInfo = GetInfo(hit, direction, distance, axis, index);
            return hit;
        }

        private bool MoveBox(
            int node, float3 start, float size,
            Box origin, float3 direction, float3 inverse, int layerMask,
            ref float distance, out int hitAxis, out int hitIndex
        ) {
            hitAxis = -1;
            hitIndex = -1;
            if (node == -1) return false;
            
            // Raycast in all colliders in this node
            for (int i = octree[9 * node]; i != -1; i = colliders[i].next) {
                if ((layerMask & 1 << colliders[i].layer) == 0) continue;
                int axis = 0;
                bool hit = colliders[i].type switch {
                    ColliderType.Mesh => meshColliders[colliders[i].index].MoveBox(origin, direction, inverse, ref distance, out axis),
                    ColliderType.Box => boxColliders[colliders[i].index].MoveBox(origin, inverse, ref distance, out axis),
                    _ => false
                };
                if (hit) {
                    hitAxis = axis;
                    hitIndex = i;
                }
            }

            // Pre-compute distances
            float childSize = size / 2;
            float3 center = start + childSize;
            float3 minDistances1 = (start - origin.max) * inverse;
            float3 maxDistances1 = (center - origin.min) * inverse;
            float3 minDistances2 = (center - origin.max) * inverse;
            float3 maxDistances2 = (start + size - origin.min) * inverse;
            bool3 sign = inverse > 0;
            float3 entryDistances1 = math.select(maxDistances1, minDistances1, sign);
            float3 exitDistances1 = math.select(minDistances1, maxDistances1, sign);
            float3 entryDistances2 = math.select(maxDistances2, minDistances2, sign);
            float3 exitDistances2 = math.select(minDistances2, maxDistances2, sign);

            // Move into children traversed by the movement
            for (int x = 0; x <= 1; x++) {
                for (int y = 0; y <= 1; y++) {
                    for (int z = 0; z <= 1; z++) {
                        bool3 side = new(x != 0, y != 0, z != 0);
                        float3 entryDistances = math.select(entryDistances1, entryDistances2, side);
                        float3 exitDistances = math.select(exitDistances1, exitDistances2, side);
                        float maxEntryDistance = 0;
                        float minExitDistance = distance;
                        for (int i = 0; i < 3; i++) {
                            if (entryDistances[i] > maxEntryDistance) {
                                maxEntryDistance = entryDistances[i];
                            }
                            if (exitDistances[i] < minExitDistance) {
                                minExitDistance = exitDistances[i];
                            }
                        }
                        if (minExitDistance >= maxEntryDistance) { // Child is traversed by the movement
                            int childNode = octree[9 * node + 1 + math.bitmask(new bool4(side, false))];
                            float3 childStart = math.select(start, start + childSize, side);
                            if (MoveBox(childNode, childStart, childSize, origin, direction, inverse, layerMask, ref distance, out int axis, out int index)) {
                                hitAxis = axis;
                                hitIndex = index;
                            }
                        }
                    }
                }
            }

            return hitAxis != -1;
        }


        private RaycastHit GetInfo(bool hit, float3 direction, float distance, int axis, int index) {
            if (hit) {
                float3 normal = 0;
                normal[axis] = -math.sign(direction[axis]);
                return new RaycastHit(direction * distance, normal, colliders[index].type, colliders[index].index);
            }
            else return new RaycastHit(0, 0, ColliderType.None, -1);
        }
        
        
        [BurstCompile]
        public static int AddMeshCollider(ref PhysicsData @this, in TransformedOctree octree, int layer)
            => @this.AddMeshCollider(octree, layer);

        [BurstCompile]
        public static int AddBoxCollider(ref PhysicsData @this, in Box box, int layer)
            => @this.AddBoxCollider(box, layer);

        [BurstCompile]
        public static int RemoveMeshCollider(ref PhysicsData @this, int index, int swapIndex)
            => @this.RemoveMeshCollider(index, swapIndex);

        [BurstCompile]
        public static int RemoveBoxCollider(ref PhysicsData @this, int index, int swapIndex)
            => @this.RemoveBoxCollider(index, swapIndex);

        [BurstCompile]
        public static bool Raycast(ref PhysicsData @this, in float3 origin, in float3 direction, float maxDistance, int layerMask, out RaycastHit hitInfo)
            => @this.Raycast(origin, direction, maxDistance, layerMask, out hitInfo);

        [BurstCompile]
        public static bool MoveBox(ref PhysicsData @this, in Box origin, in float3 direction, float maxDistance, int layerMask, out RaycastHit hitInfo)
            => @this.MoveBox(origin, direction, maxDistance, layerMask, out hitInfo);


        public readonly struct LinkedCollider {
            public readonly ColliderType type;
            public readonly int index;
            public readonly int layer;
            public readonly int next;

            public LinkedCollider(ColliderType type, int index, int layer, int next) {
                this.type = type;
                this.index = index;
                this.layer = layer;
                this.next = next;
            }
        }


        public readonly struct RaycastHit {
            public readonly float3 movement;
            public readonly float3 normal;
            public readonly ColliderType type;
            public readonly int index;

            public RaycastHit(float3 movement, float3 normal, ColliderType type, int index) {
                this.movement = movement;
                this.normal = normal;
                this.type = type;
                this.index = index;
            }
        }
    }


    internal enum ColliderType {
        Mesh,
        Box,
        None
    }

}