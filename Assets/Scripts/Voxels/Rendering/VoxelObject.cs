using System;
using UnityEngine;
using Voxels.Collections;

namespace Voxels.Rendering {
    
    [ExecuteInEditMode]
    public class VoxelObject : MonoBehaviour {
        [SerializeField] private TextAsset voxelsAsset;
        [NonSerialized] public VoxelColumns voxels;
        public Vector3 offset;

        private int id = -1;
        private Matrix4x4 prevMatrix;

        private void Update() {
            if (!voxels.Created && voxelsAsset) {
                voxels = new(voxelsAsset);
            }
            Matrix4x4 matrix = transform.localToWorldMatrix;
            if (id == -1 && voxels.Created) {
                id = VoxelObjects.Instance.AddObject(voxels, offset, matrix);
            }
            if (id != -1 && matrix != prevMatrix) {
                VoxelObjects.Instance.SetTransform(id, matrix);
            }
            prevMatrix = matrix;
        }
    }

}