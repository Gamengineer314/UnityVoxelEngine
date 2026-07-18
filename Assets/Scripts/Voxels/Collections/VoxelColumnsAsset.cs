using UnityEngine;
using Unity.Collections;

namespace Voxels.Collections {
    
    /// <summary>
    /// Asset that contains a VoxelColumns struct imported from a voxel model
    /// </summary>
    [PreferBinarySerialization]
    public class VoxelColumnsAsset : ScriptableObject {
        [SerializeField] private int sizeX, sizeZ;
        [SerializeField] private VoxelColumns.Column[] columns;
        [SerializeField] private int[] startIndices;
        public VoxelColumns voxels { get; private set; }

        internal void Init(VoxelColumns voxels) {
            this.voxels = voxels;
            sizeX = voxels.sizeX;
            sizeZ = voxels.sizeZ;
            columns = voxels.columns.ToArray();
            startIndices = voxels.startIndices.ToArray();
        }

        private void OnEnable() {
            if (columns is not null && !voxels.columns.IsCreated) {
                voxels = new VoxelColumns(sizeX, sizeZ, new(columns, Allocator.Persistent), new(startIndices, Allocator.Persistent));
            }
        }

        private void OnDisable() {
            voxels.Dispose();
        }

#if UNITY_EDITOR
        ~VoxelColumnsAsset() { // Dispose in destructor for the cases where OnDisable isn't called in the editor
            voxels.Dispose();
        }
#endif
    }

}