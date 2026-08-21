using Unity.Mathematics;

namespace Voxels.Physics {
    
    /// <summary>
    /// Axis-aligned box, similar to Bounds
    /// </summary>
    public readonly struct Box {
        public readonly float3 min, max;

        public readonly float3 Center => (max + min) / 2;
        public readonly float3 Size => max - min;

        public Box(float3 min, float3 max) {
            this.min = min;
            this.max = max;
        }

        public static Box operator +(Box box, float3 offset)
            => new(box.min + offset, box.max + offset);

        public static Box operator +(float3 offset, Box box)
            => box + offset;

        public static Box operator -(Box box, float3 offset)
            => box + -offset;

        public override string ToString()
            => $"Box(({min.x}, {min.y}, {min.z}), ({max.x}, {max.y}, {max.z}))";


        /// <summary>
        /// Raycast query
        /// </summary>
        /// <param name="origin">Origin of the ray</param>
        /// <param name="inverse">Pre-computed inverse of the ray's direction</param>
        /// <param name="distance">
        /// Input: Maximum distance between the origin and the hit point.
        /// Output: Actual distance.
        /// </param>
        /// <param name="axis">Axis of the face that was hit</param>
        /// <returns>Whether the ray hit the box</returns>
        internal readonly bool Raycast(float3 origin, float3 inverse, ref float distance, out int axis) {
            float3 minDistances = (min - origin) * inverse;
            float3 maxDistances = (max - origin) * inverse;
            float3 entryDistances = math.select(minDistances, maxDistances, maxDistances < minDistances);
            float3 exitDistances = math.select(minDistances, maxDistances, maxDistances > minDistances);
            float maxEntryDistance = 0;
            float minExitDistance = distance;
            axis = 0;
            for (int i = 0; i < 3; i++) {
                if (entryDistances[i] > maxEntryDistance) {
                    maxEntryDistance = entryDistances[i];
                    axis = i;
                }
                if (exitDistances[i] < minExitDistance) {
                    minExitDistance = exitDistances[i];
                }
            }
            if (minExitDistance >= maxEntryDistance) {
                distance = maxEntryDistance;
                return true;   
            }
            return false;
        }


        /// <summary>
        /// Move query with a box shape
        /// </summary>
        /// <param name="origin">Start position of the box</param>
        /// <param name="inverse">Pre-computed inverse of [direction]</param>
        /// <param name="distance">
        /// Input: Maximum distance between the origin and the hit point.
        /// Output: Actual distance.
        /// </param>
        /// <param name="axis">Axis of the face that was hit</param>
        /// <returns>Whether the box hit this box</returns>
        internal readonly bool MoveBox(Box origin, float3 inverse, ref float distance, out int axis)
            => new Box(min - origin.Size, max).Raycast(origin.min, inverse, ref distance, out axis);
    }

}