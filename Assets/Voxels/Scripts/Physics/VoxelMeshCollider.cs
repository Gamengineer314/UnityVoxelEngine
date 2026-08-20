using UnityEngine;
using Voxels.Collections;
using Voxels.Rendering;

namespace Voxels.Physics {
    
    [RequireComponent(typeof(VoxelMesh))]
    public class VoxelMeshCollider : MonoBehaviour {
        [SerializeField] private VoxelColumnsAsset voxelsAsset;
        public GenerationParameters parameters;
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
                Generate();
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
                VoxelPhysics.Instance.generator.Schedule(voxels, parameters.jobHorizontalSize, parameters.asynchronousGeneration, AddToPhysics);
            }
        }

        private void AddToPhysics(VoxelColumns voxels) {
            if (!this || generated || !voxels.Equals(this.voxels)) return;
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


        /// <summary>
        /// Complete the generation of this object's octree
        /// </summary>
        public void CompleteGeneration() => VoxelPhysics.Instance.generator.Complete(voxels);
    }

}