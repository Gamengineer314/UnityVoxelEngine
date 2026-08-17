using UnityEngine;

namespace Voxels.Physics {
    
    public class VoxelBoxCollider : MonoBehaviour {
        [SerializeField] private Vector3 center;
        [SerializeField] private Vector3 size;
        internal int index; // Index of the collider in physics data


        public Vector3 Center {
            get => center;
        }

        public Vector3 Size {
            get => size;
        }


        private void Start() {
            VoxelPhysics.Instance.AddBoxCollider(this);
        }

        private void OnDestroy() {
            
        }
    }

}