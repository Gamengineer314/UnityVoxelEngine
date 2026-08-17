using System;
using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using Voxels.Collections;

namespace Voxels.Physics {
    
    /// <summary>
    /// Octree of voxels without data
    /// </summary>
    [BurstCompile]
    internal struct BinaryOctree : IDisposable {
        private const int full = -1;
        private const int empty = -2;

        private NativeList<int> children; // 8 ints per node pointing to its children
        private readonly int root;
        private readonly int size;
        public readonly Box bounds; // Bounding box in world coordinates

        // Local to world transformation
        private readonly float3 position;
        private readonly int3 transpose;
        private readonly float3 scale;


        /// <summary>
        /// Create an octree from a voxels asset
        /// </summary>
        /// <param name="voxels">The voxels</param>
        public BinaryOctree(VoxelColumns voxels) {
            children = new NativeList<int>(Allocator.Persistent);
            root = empty;
            size = math.ceilpow2(math.max(voxels.size.x, math.max(voxels.size.y, voxels.size.z)));
            bounds = new Box(voxels.offset, voxels.offset + voxels.size);
            position = voxels.offset;
            scale = 1;
            transpose = new int3(0, 1, 2);
            Add(in voxels, ref children, ref root, size);
        }

        /// <summary>
        /// Create a view of an octree with a transform
        /// </summary>
        /// <param name="octree">The other octree</param>
        /// <param name="localTransform">Transformation from the other octree to the view</param>
        public BinaryOctree(BinaryOctree octree, Transform transform) {
            children = octree.children;
            root = octree.root;
            size = octree.size;
            position = transform.position;
            float3 transposeComponents = transform.rotation * new float3(1, 2, 3); // Rotate (1, 2, 3) to see where each axis ends up
            scale = transform.lossyScale * math.sign(transposeComponents);
            transpose = (int3)math.round(math.abs(transposeComponents)) - 1;
            bounds = default;
            position = ToWorld(octree.position, true); // Add asset offset
            float3 min = ToWorld(octree.bounds.min - octree.position, true);
            float3 max = ToWorld(octree.bounds.max - octree.position, true);
            bounds = new Box(math.min(min, max), math.max(min, max));
        }

        public void Dispose() {
            children.Dispose();
        }


        [BurstCompile]
        private static void Add(in VoxelColumns voxels, ref NativeList<int> children, ref int root, int size) {
            int reusable = -1;
            for (int z = 0; z < voxels.size.z; z++) {
                for (int x = 0; x < voxels.size.x; x++) {
                    foreach (Voxel voxel in voxels.GetColumn(x, z)) {
                        if (Voxel.Color32Equals(voxel.color, Voxel.ghost)) continue;
                        int3 coords = new(x, voxel.y, z);
                        root = Add(root, coords, size >> 1, children, ref reusable);
                    }
                }
            }
        }

        private static int Add(int node, int3 coords, int childSize, NativeList<int> children, ref int reusable) {
            // Add node if empty
            if (node == empty) {
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

            // Find child
            bool3 side = coords >= childSize;
            int childNode = 8 * node + math.bitmask(new bool4(side, false));

            // Add in child
            int child;
            if (childSize == 1) {
                child = full;
            }
            else {
                int3 childCoords = math.select(coords, coords - childSize, side);
                child = Add(children[childNode], childCoords, childSize >> 1, children, ref reusable);
            }
            children[childNode] = child;

            // Remove node if all children are full
            for (int i = 0; i < 8; i++) {
                if (children[8 * node + i] != full) return node;
            }
            children[8 * node] = reusable;
            reusable = node;
            return full;
        }


        /// <summary>
        /// Raycast query
        /// </summary>
        /// <param name="origin">Origin of the ray</param>
        /// <param name="direction">Direction of the ray</param>
        /// <param name="maxDistance">Maximum distance between the origin and the hit point</param>
        /// <param name="hitDistance">Distance between the origin and the hit point</param>
        /// <param name="axis">Axis of the face that was hit</param>
        /// <returns>Whether the ray hit the box</returns>
        public readonly bool Raycast(float3 origin, float3 direction, float maxDistance, out float hitDistance, out int axis) {
            // Raycast the bounds in world coordinates
            if (!bounds.Raycast(origin, direction, maxDistance, out hitDistance, out axis)) {
                return false;
            }

            // Raycast the voxels in local coordinates
            float3 localOrigin = ToLocal(origin + direction * hitDistance, true);
            float3 localDirection = ToLocal(direction, false);
            float localMaxDistance = maxDistance - hitDistance;
            int localAxis = ToLocal(axis);
            bool hit = Raycast(
                root, localAxis, size >> 1,
                localOrigin, localDirection, localMaxDistance, out float localHitDistance, out int localHitAxis
            );
            hitDistance = localHitDistance + hitDistance;
            axis = ToWorld(localHitAxis);
            return hit;
        }

        private readonly bool Raycast(
            int node, int axis, int childSize,
            float3 origin, float3 direction, float maxDistance, out float hitDistance, out int hitAxis
        ) {
            if (node == empty) { // No hit in this node
                hitDistance = float.PositiveInfinity;
                hitAxis = -1;
                return false;
            }
            if (node == full) { // Hit at this point
                hitDistance = 0;
                hitAxis = axis;
                return true;
            }

            // Raycast in children traversed by the ray
            float3 distances = (childSize - origin) / direction;
            bool3 side = origin > childSize;
            float addedDistance = 0;
            float3 childOrigin = origin;
            for (int i = 0; i < 4; i++) {
                // Raycast in child
                int childNode = children[8 * node + math.bitmask(new bool4(side, false))];
                float3 offset = math.select(0, childSize, side);
                if (Raycast(childNode, axis, childSize >> 1, childOrigin - offset, direction, maxDistance - addedDistance, out hitDistance, out hitAxis)) {
                    hitDistance += addedDistance;
                    return true;
                }

                // Find next child
                axis = -1;
                addedDistance = maxDistance;
                for (int j = 0; j < 3; j++) {
                    float d = distances[j];
                    float3 intersection = origin + d * direction;
                    intersection[j] = childSize;
                    if (d >= 0 && d < addedDistance && math.all(intersection >= 0 & intersection <= 2 * childSize)) {
                        addedDistance = d;
                        axis = j;
                        childOrigin = intersection;
                    }
                }
                if (axis == -1) break;
                side[axis] = !side[axis];
                distances[axis] = float.PositiveInfinity;
            }

            // No hit in this node
            hitDistance = float.PositiveInfinity;
            hitAxis = -1;
            return false;
        }


        /// <summary>
        /// Local to world conversion
        /// </summary>
        /// <param name="vector">Local vector</param>
        /// <param name="addPosition">Whether the vector is a point</param>
        /// <returns>World vector</returns>
        private readonly float3 ToWorld(float3 vector, bool isPoint) {
            vector *= scale;
            vector = new float3(vector[transpose.x], vector[transpose.y], vector[transpose.z]);
            if (isPoint) vector += position;
            return vector;
        }

        private readonly int ToWorld(int axis)
            => transpose.x == axis ? 0 : transpose.y == axis ? 1 : 2;

        /// <summary>
        /// World to local conversion
        /// </summary>
        /// <param name="vector">World vector</param>
        /// <param name="addPosition">Whether the vector is a point</param>
        /// <returns>Local vector</returns>
        private readonly float3 ToLocal(float3 vector, bool isPoint) {
            if (isPoint) vector -= position;
            float3 transposed = 0;
            transposed[transpose.x] = vector.x;
            transposed[transpose.y] = vector.y;
            transposed[transpose.z] = vector.z;
            vector = transposed;
            vector /= scale;
            return vector;
        }

        private readonly int ToLocal(int axis)
            => transpose[axis];
    }

}