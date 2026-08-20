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
            float3 min = ToWorld(0, true);
            float3 max = ToWorld(voxels.size, true);
            bounds = new Box(math.min(min, max), math.max(min, max));
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
            if (!bounds.Raycast(origin, direction, inverse, ref boundsDistance, out axis)) {
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
            if (node == -1) return false;
            if (node == -2) { // Hit at this point
                distance = 0;
                return true;
            }

            // Raycast in children traversed by the ray
            int childSize = size / 2;
            float3 distances = (childSize - origin) * inverse;
            distances = math.select(distances, float.PositiveInfinity, distances < 0);
            bool3 side = origin > childSize;
            float addedDistance = 0;
            float3 childOrigin = origin;
            for (int i = 0; i < 4; i++) {
                // Raycast in child
                int childNode = children[8 * node + math.bitmask(new bool4(side, false))];
                float3 offset = math.select(0, childSize, side);
                float childDistance = distance - addedDistance;
                if (Raycast(childNode, childSize, childOrigin - offset, direction, inverse, ref childDistance, ref axis)) {
                    distance = childDistance + addedDistance;
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
                if (addedDistance > distance) break;
                childOrigin = origin + direction * addedDistance;
                childOrigin[axis] = childSize;
                if (!math.all(childOrigin >= 0 & childOrigin <= size)) break;
                side[axis] = !side[axis];
                distances[axis] = float.PositiveInfinity;
            }
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