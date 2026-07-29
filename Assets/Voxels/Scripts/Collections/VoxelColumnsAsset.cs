using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Collections.LowLevel.Unsafe;

namespace Voxels.Collections {
    
    /// <summary>
    /// Asset that contains a VoxelColumns struct imported from a voxel model
    /// </summary>
    [PreferBinarySerialization]
    public class VoxelColumnsAsset : ScriptableObject {
        [SerializeField] private int sizeX, sizeZ;
        [SerializeField] private float3 offset;
        [SerializeField] [HideInInspector] private VoxelColumns.Column[] columns;
        [SerializeField] [HideInInspector] private int[] startIndices;
        public VoxelColumns voxels { get; private set; }

        internal void Init(VoxelColumns voxels) {
            sizeX = voxels.sizeX;
            sizeZ = voxels.sizeZ;
            offset = voxels.offset;
            columns = voxels.columns.ToArray();
            startIndices = voxels.startIndices.ToArray();
        }

        private void OnEnable() {
            if (columns != null) {
                voxels = new VoxelColumns(sizeX, sizeZ, offset, new(columns, Allocator.Persistent), new(startIndices, Allocator.Persistent));
#if UNITY_EDITOR
                Editor.EditorDisposer.disposables.Add(voxels); // Dispose on domain reload because OnDisable isn't always called in the editor
#endif
            }
        }

#if !UNITY_EDITOR
        private void OnDisable() {
            voxels.Dispose();
        }
#endif
    }

}