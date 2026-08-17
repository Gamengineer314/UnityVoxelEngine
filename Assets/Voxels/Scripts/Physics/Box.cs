using Unity.Mathematics;

namespace Voxels.Physics {
    
    /// <summary>
    /// Axis-aligned box, similar to Bounds
    /// </summary>
    public readonly struct Box {
        public readonly float3 min, max;

        public Box(float3 min, float3 max) {
            this.min = min;
            this.max = max;
        }

        public static Box operator +(Box box, float3 offset)
            => new(box.min + offset, box.max + offset);

        public static Box operator -(Box box, float3 offset)
            => new(box.min - offset, box.max - offset);

        public override string ToString()
            => $"Box(({min.x}, {min.y}, {min.z}), ({max.x}, {max.y}, {max.z}))";


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
        /// <returns>Whether the ray hit the box</returns>
        internal readonly bool Raycast(float3 origin, float3 direction, float3 inverse, ref float distance, out int axis) {
            if (math.all(origin >= min & origin <= max)) { // Already inside bounds
                distance = 0;
                axis = 0;
                return true;
            }

            float3 planes = math.select(max, min, inverse > 0);
            float3 distances = (planes - origin) * inverse;
            float maxDistance = float.NegativeInfinity;
            axis = 0;
            for (int i = 0; i < 3; i++) {
                if (distances[i] > maxDistance) {
                    maxDistance = distances[i];
                    axis = i;
                }
            }
            if (maxDistance < 0 || maxDistance > distance) return false;
            float3 point = origin + maxDistance * direction;
            point[axis] = planes[axis];
            if (!math.all(point >= min & point <= max)) return false;
            distance = maxDistance;
            return true;
        }
    }

}