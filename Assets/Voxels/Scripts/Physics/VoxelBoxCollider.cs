using UnityEngine;

namespace Voxels.Physics {
    
    public class VoxelBoxCollider : MonoBehaviour {
        [SerializeField] private Vector3 center;
        [SerializeField] private Vector3 size;
        internal int index = -1; // Index of the collider in physics data
        internal Vector3 prevTransform;
        
        public Box Box => new(transform.position + center - size / 2, transform.position + center + size / 2);


        public Vector3 Center {
            get => center;
            set {
                VoxelPhysics.Instance.RemoveBoxCollider(this);
                center = value;
                VoxelPhysics.Instance.AddBoxCollider(this);
            }
        }

        public Vector3 Size {
            get => size;
            set {
                VoxelPhysics.Instance.RemoveBoxCollider(this);
                size = value;
                VoxelPhysics.Instance.AddBoxCollider(this);
            }
        }

#if UNITY_EDITOR
        private void OnValidate() {
            if (VoxelPhysics.Instance) {
                VoxelPhysics.Instance.RemoveBoxCollider(this);
                VoxelPhysics.Instance.AddBoxCollider(this);
            }
        }
#endif


        private void Start() {
            if (index == -1) VoxelPhysics.Instance.AddBoxCollider(this);
        }

        private void OnEnable() {
            if (index == -1 && VoxelPhysics.Instance) VoxelPhysics.Instance.AddBoxCollider(this);
        }

        private void OnDisable() {
            if (VoxelPhysics.Instance) VoxelPhysics.Instance.RemoveBoxCollider(this);
        }
    }

}