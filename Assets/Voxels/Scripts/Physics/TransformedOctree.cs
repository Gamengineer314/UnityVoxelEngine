using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using Voxels.Collections;

namespace Voxels.Physics {
    
    /// <summary>
    /// Octree of voxels with a transform
    /// </summary>
    internal readonly struct TransformedOctree {
        private readonly NativeList<int> children; // 8 ints per node pointing to its children
        private readonly int root;
        private readonly int size;
        public readonly Box bounds; // Bounding box in world coordinates

        // Local to world transformation
        private readonly float3 position;
        private readonly int3 transpose;
        private readonly float3 scale;


        public TransformedOctree(OctreeBuilder octree, VoxelColumns voxels, Transform transform) {
            children = octree.children;
            root = octree.root;
            size = octree.size;
            position = transform.position;
            float3 transposeComponents = transform.rotation * new float3(1, 2, 3); // Rotate (1, 2, 3) to see where each axis ends up
            scale = transform.lossyScale * math.sign(transposeComponents);
            transpose = (int3)math.round(math.abs(transposeComponents)) - 1;
            bounds = default;
            position = ToWorld(voxels.offset, true); // Add offset
            bounds = ToWorld(new Box(0, voxels.size));
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

        private readonly Box ToWorld(Box box) {
            float3 min = ToWorld(box.min, true);
            float3 max = ToWorld(box.max, true);
            return new Box(math.min(min, max), math.max(min, max));
        }

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

        private readonly Box ToLocal(Box box) {
            float3 min = ToLocal(box.min, true);
            float3 max = ToLocal(box.max, true);
            return new Box(math.min(min, max), math.max(min, max));
        }


        /// <summary>
        /// Raycast query
        /// </summary>
        /// <param name="origin">Origin of the ray</param>
        /// <param name="direction">Direction of the ray</param>
        /// <param name="inverse">Pre-computed inverse of [direction]</param>
        /// <param name="distance">
        /// Input: Maximum distance between the origin and the hit point.
        /// Output: Actual distance.
        /// </param>
        /// <param name="axis">Axis of the face that was hit</param>
        /// <returns>Whether the ray hit a voxel</returns>
        public readonly bool Raycast(float3 origin, float3 direction, float3 inverse, ref float distance, out int axis) {
            // Raycast the bounds in world coordinates
            float boundsDistance = distance;
            if (!bounds.Raycast(origin, inverse, ref boundsDistance, out axis)) {
                return false;
            }

            // Raycast the voxels in local coordinates
            float3 localOrigin = ToLocal(origin + direction * boundsDistance, true);
            float3 localDirection = ToLocal(direction, false);
            float localDistance = distance - boundsDistance;
            int localAxis = ToLocal(axis);
            bool hit = Raycast(root, size, localOrigin, localDirection, 1 / localDirection, ref localDistance, ref localAxis);
            distance = localDistance + boundsDistance;
            axis = ToWorld(localAxis);
            return hit;
        }

        private readonly bool Raycast(int node, int size, float3 origin, float3 direction, float3 inverse, ref float distance, ref int axis) {
            if (node == OctreeBuilder.empty) return false;
            if (node == OctreeBuilder.full) { // Hit at this point
                distance = 0;
                return true;
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
            int childSize = size / 2;
            bool3 side = origin > childSize;
            float3 distances = (childSize - origin) * inverse;
            distances = math.select(float.PositiveInfinity, distances, distances >= 0);
            float addedDistance = 0;
            int nextAxis = axis;
            for (int i = 0; i < 4; i++) {
                // Raycast in child
                int childNode = children[8 * node + math.bitmask(new bool4(side, false))];
                float3 childOrigin = origin + direction * addedDistance - math.select(0, childSize, side);
                float childDistance = distance - addedDistance;
                axis = nextAxis;
                if (Raycast(childNode, childSize, childOrigin, direction, inverse, ref childDistance, ref axis)) {
                    distance = childDistance + addedDistance;
                    return true;
                }

                // Find next child
                nextAxis = 0;
                addedDistance = distances[0];
                for (int j = 1; j < 3; j++) {
                    if (distances[j] < addedDistance) {
                        nextAxis = j;
                        addedDistance = distances[j];
                    }
                }
                if (addedDistance > minExitDistance) break;
                side[nextAxis] = !side[nextAxis];
                distances[nextAxis] = float.PositiveInfinity;
            }
            return false;
        }


        /// <summary>
        /// Move query with a box shape
        /// </summary>
        /// <param name="origin">Start position of the box</param>
        /// <param name="direction">Direction of the box</param>
        /// <param name="inverse">Pre-computed inverse of [direction]</param>
        /// <param name="distance">
        /// Input: Maximum distance between the origin and the hit point.
        /// Output: Actual distance.
        /// </param>
        /// <param name="axis">Axis of the face that was hit</param>
        /// <returns>Whether the box hit a voxel</returns>
        internal readonly bool MoveBox(Box origin, float3 direction, float3 inverse, ref float distance, out int axis) {
            // Move into the bounds in world coordinates
            float boundsDistance = distance;
            if (!bounds.MoveBox(origin, inverse, ref boundsDistance, out axis)) {
                return false;
            }

            // Move into the voxels in local coordinates
            Box localOrigin = ToLocal(origin + direction * boundsDistance);
            float3 localDirection = ToLocal(direction, false);
            float localDistance = distance - boundsDistance;
            int localAxis = ToLocal(axis);
            bool hit = MoveBox(root, size, localOrigin, localDirection, 1 / localDirection, ref localDistance, ref localAxis);
            distance = localDistance + boundsDistance;
            axis = ToWorld(localAxis);
            return hit;
        }

        private readonly bool MoveBox(int node, int size, Box origin, float3 direction, float3 inverse, ref float distance, ref int axis) {
            if (node == OctreeBuilder.empty) return false;
            if (node == OctreeBuilder.full) { // Hit at this point
                distance = 0;
                return true;
            }

            // Pre-compute distances
            int childSize = size / 2;
            float3 minDistances1 = -origin.max * inverse;
            float3 maxDistances1 = (childSize - origin.min) * inverse;
            float3 minDistances2 = (childSize - origin.max) * inverse;
            float3 maxDistances2 = (size - origin.min) * inverse;
            bool3 sign = inverse > 0;
            float3 entryDistances1 = math.select(maxDistances1, minDistances1, sign);
            float3 exitDistances1 = math.select(minDistances1, maxDistances1, sign);
            float3 entryDistances2 = math.select(maxDistances2, minDistances2, sign);
            float3 exitDistances2 = math.select(minDistances2, maxDistances2, sign);

            // Move into children traversed by the movement
            bool hit = false;
            for (int x = 0; x <= 1; x++) {
                for (int y = 0; y <= 1; y++) {
                    for (int z = 0; z <= 1; z++) {
                        bool3 side = new(x != 0, y != 0, z != 0);
                        float3 entryDistances = math.select(entryDistances1, entryDistances2, side);
                        float3 exitDistances = math.select(exitDistances1, exitDistances2, side);
                        float maxEntryDistance = 0;
                        float minExitDistance = distance;
                        int entryAxis = 0;
                        for (int i = 0; i < 3; i++) {
                            if (entryDistances[i] > maxEntryDistance) {
                                maxEntryDistance = entryDistances[i];
                                entryAxis = i;
                            }
                            if (exitDistances[i] < minExitDistance) {
                                minExitDistance = exitDistances[i];
                            }
                        }
                        if (minExitDistance >= maxEntryDistance) { // Child is traversed by the movement
                            int childNode = children[8 * node + math.bitmask(new bool4(side, false))];
                            Box childOrigin = origin + direction * maxEntryDistance - math.select(0, childSize, side);
                            float childDistance = distance - maxEntryDistance;
                            if (MoveBox(childNode, childSize, childOrigin, direction, inverse, ref childDistance, ref entryAxis)) {
                                distance = childDistance + maxEntryDistance;
                                axis = entryAxis;
                                hit = true;
                            }
                        }
                    }
                }
            }
            return hit;
        }
    }

}