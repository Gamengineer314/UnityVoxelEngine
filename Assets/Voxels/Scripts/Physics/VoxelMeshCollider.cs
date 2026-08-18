using UnityEngine;
using Voxels.Collections;
using Voxels.Rendering;

namespace Voxels.Physics {
    
    [RequireComponent(typeof(VoxelMesh))]
    public class VoxelMeshCollider : MonoBehaviour {
        [SerializeField] private VoxelColumnsAsset voxelsAsset;
        internal VoxelColumns voxels;
        internal int index = -1; // Index of the collider in physics data
        internal Matrix4x4 prevTransform;
        public bool generated { get; private set; }


        public VoxelColumns Voxels {
            get => voxelsAsset ? voxelsAsset.voxels : voxels;
            set {
                RemoveFromPhysics();
                voxelsAsset = null;
                voxels = value;
                AddToPhysics();
            }
        }

#if UNITY_EDITOR
        private void Reset() {
            if (TryGetComponent(out VoxelMesh mesh)) {
                voxelsAsset = mesh.voxelsAsset;
            }
        }

        private void OnValidate() {
            if (VoxelPhysics.Instance) {
                RemoveFromPhysics();
                Generate();
            }
        }
#endif


        private void Start() {
            Generate();
        }

        private void OnEnable() {
            if (generated) VoxelPhysics.Instance.AddMeshCollider(this);
        }

        private void OnDisable() {
            if (index != -1 && VoxelPhysics.Instance) VoxelPhysics.Instance.RemoveMeshCollider(this);
        }

        private void OnDestroy() {
            if (VoxelPhysics.Instance) RemoveFromPhysics();
        }


        private void Generate() {
            if (voxelsAsset) voxels = voxelsAsset.voxels;
            if (voxels.IsCreated) {
                AddToPhysics();
            }
        }

        private void AddToPhysics() {
            VoxelPhysics.Instance.AddReference(voxels);
            generated = true;
            if (isActiveAndEnabled) {
                VoxelPhysics.Instance.AddMeshCollider(this);
            }
        }

        private void RemoveFromPhysics() {
            if (generated) {
                generated = false;
                VoxelPhysics.Instance.RemoveReference(voxels);
            }
            if (index != -1) VoxelPhysics.Instance.RemoveMeshCollider(this);
        }
    }

}