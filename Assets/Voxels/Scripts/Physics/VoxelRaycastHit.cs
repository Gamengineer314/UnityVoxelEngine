using UnityEngine;

namespace Voxels.Physics {
    
    public readonly struct VoxelRaycastHit {
        public readonly Vector3 movement; // Movement between the origin and the hit point
        public readonly Vector3 normal; // Normal of the face that was hit
        public readonly GameObject collider; // Object that was hit
        
        public VoxelRaycastHit(Vector3 movement, Vector3 normal, GameObject collider) {
            this.movement = movement;
            this.normal = normal;
            this.collider = collider;
        }
    }

}