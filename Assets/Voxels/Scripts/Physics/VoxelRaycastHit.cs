using UnityEngine;

namespace Voxels.Physics {
    
    public readonly struct VoxelRaycastHit {
        public readonly float distance; // Distance between the origin and the hit point
        public readonly Vector3 normal; // Normal of the face that was hit
        public readonly VoxelCollider collider; // Collider that was hit
        
        public VoxelRaycastHit(float distance, Vector3 normal, VoxelCollider collider) {
            this.distance = distance;
            this.normal = normal;
            this.collider = collider;
        }
    }

}