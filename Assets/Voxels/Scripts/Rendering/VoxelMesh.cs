using System;
using UnityEngine;
using Voxels.Collections;

namespace Voxels.Rendering {
    
    [ExecuteInEditMode]
    public class VoxelMesh : MonoBehaviour {
        [SerializeField] internal VoxelColumnsAsset voxelsAsset;
        [SerializeField] internal GenerationParameters parameters;
        [SerializeField] internal Material material;
        private GenerationCommand command;

        public VoxelColumns Voxels {
            set {
                command = new GenerationCommand(value, parameters, material);
                VoxelRenderer.Instance.generator.Schedule(command, parameters.jobHorizontalSize, AddToLayer);
            }
        }

        internal void Start() {
            if (voxelsAsset) {
                command = new GenerationCommand(voxelsAsset.voxels, parameters, material);
                VoxelRenderer.Instance.generator.Schedule(command, parameters.jobHorizontalSize, AddToLayer);
            }
        }

        /// <summary>
        /// Add this object to its layer
        /// </summary>
        private void AddToLayer() => VoxelLayer.GetLayer(gameObject.layer, material).AddObject(command, transform);

        /// <summary>
        /// Complete the generation of this object's mesh
        /// </summary>
        public void CompleteGeneration() => VoxelRenderer.Instance.generator.Complete(command);
    }

}