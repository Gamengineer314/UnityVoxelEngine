using System;
using UnityEngine;
using Voxels.Collections;

namespace Voxels.Rendering {
    
    [ExecuteInEditMode]
    public class VoxelMesh : MonoBehaviour {
        [SerializeField] private TextAsset voxelsAsset;
        [SerializeField] private Material material;
        [SerializeField] internal Vector3 offset;
        internal VoxelColumns voxels;

        internal void Start() {
            if (voxelsAsset) {
                voxels = new(voxelsAsset);
                AddToLayer();
            }
        }

        /// <summary>
        /// Set the voxels of this object.
        /// The voxels won't be disposed when this object is destroyed.
        /// </summary>
        /// <param name="voxels">The voxels</param>
        /// <param name="offset">Offset to add to the positions</param>
        public void SetVoxels(VoxelColumns voxels, Vector3 offset) {
            if (this.voxels.IsCreated) throw new InvalidOperationException("VoxelMesh voxels can only be set once");
            this.voxels = voxels;
            this.offset = offset;
            AddToLayer();
        }

        /// <summary>
        /// Add this object to its layer
        /// </summary>
        private void AddToLayer() => VoxelLayer.GetLayer(gameObject.layer, material).AddObject(this);

        /// <summary>
        /// Complete the generation of this object's mesh
        /// </summary>
        public void CompleteGeneration() => VoxelLayer.GetLayer(gameObject.layer, material).CompleteGeneration(this);
    }

}