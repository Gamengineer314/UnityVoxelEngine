using UnityEngine;

namespace Voxels.Physics {
    
    public readonly struct VoxelRaycastHit {
        public readonly Vector3 point;
        public readonly Vector3 normal;
        public readonly GameObject collider;
        
        public VoxelRaycastHit(Vector3 point, Vector3 normal, GameObject collider) {
            this.point = point;
            this.normal = normal;
            this.collider = collider;
        }
    }

}