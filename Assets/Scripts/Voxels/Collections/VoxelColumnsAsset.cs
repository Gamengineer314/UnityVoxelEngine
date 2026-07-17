using UnityEngine;

namespace Voxels.Collections {
    
    /// <summary>
    /// Reference to a TextAsset that contains a VoxelColumns struct imported from a voxel model
    /// </summary>
    public class VoxelColumnsAsset : ScriptableObject {
        [SerializeField] private TextAsset asset;
        public VoxelColumns voxels { get; private set; }

        internal TextAsset Asset {
            set {
                asset = value;
                voxels = new VoxelColumns(value);
            }
        }

        private void OnEnable() {
            if (asset != null) voxels = new VoxelColumns(asset);
        }
    }

}