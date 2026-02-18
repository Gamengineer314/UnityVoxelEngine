using System;
using UnityEngine;
using Voxels.Collections;

namespace Voxels.Rendering {
    
    [ExecuteInEditMode]
    public class VoxelObject : MonoBehaviour {
        [SerializeField] private TextAsset voxelsAsset;
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
            if (this.voxels.IsCreated) throw new InvalidOperationException("VoxelObject voxels can only be set once");
            this.voxels = voxels;
            this.offset = offset;
            AddToLayer();
        }

        /// <summary>
        /// Add this object to its layer
        /// </summary>
        internal void AddToLayer() {
            int layer = gameObject.layer;
            VoxelTerrainLayer terrainLayer = VoxelTerrainLayer.GetLayer(layer);
            if (terrainLayer) terrainLayer.AddObject(this);
            else {
                VoxelObjectLayer objectLayer = VoxelObjectLayer.GetLayer(layer);
                if (objectLayer) objectLayer.AddObject(this);
            }
        }

        /// <summary>
        /// Complete all queued generations for the layer of this object
        /// </summary>
        public void Complete() {
            int layer = gameObject.layer;
            VoxelTerrainLayer terrainLayer = VoxelTerrainLayer.GetLayer(layer);
            if (terrainLayer) terrainLayer.Complete();
            else {
                VoxelObjectLayer objectLayer = VoxelObjectLayer.GetLayer(layer);
                if (objectLayer) objectLayer.Complete();
            }
        }
    }

}