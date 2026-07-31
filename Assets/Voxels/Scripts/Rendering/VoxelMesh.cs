using System;
using UnityEngine;
using Voxels.Collections;

namespace Voxels.Rendering {
    
    [ExecuteInEditMode]
    public class VoxelMesh : MonoBehaviour {
        [SerializeField] internal VoxelColumnsAsset voxelsAsset;
        [SerializeField] internal GenerationParameters parameters;
        [SerializeField] internal Material material;
        internal VoxelLayer layer;
        internal GenerationCommand command;


        public VoxelColumns Voxels {
            get => command.voxels;
            set {
                RemoveFromLayer();
                voxelsAsset = null;
                command.voxels = value;
                Generate();
            }
        }

        public GenerationParameters Parameters {
            get => parameters;
            set {
                RemoveFromLayer();
                parameters = value;
                Generate();
            }
        }

        public Material Material {
            get => material;
            set {
                RemoveFromLayer();
                material = value;
                Generate();
            }
        }

        internal void OnInspectorChanged() {
            RemoveFromLayer();
            Generate();
        }


        internal void Start() {
            if (!command.voxels.IsCreated) {
                Generate();
            }
        }

        private void OnEnable() {
            if (layer != null) {
                layer = VoxelRenderer.GetRenderer(this).GetLayer(this);
                layer.AddInstance(this);
            }
        }

        private void OnDisable() {
            if (layer != null && layer.layerBuffers.IsCreated) layer.RemoveInstance(this);
        }

        private void OnDestroy() {
            if (layer != null && layer.layerBuffers.IsCreated) RemoveFromLayer();
        }


        private void Generate() {
            if ((voxelsAsset || command.voxels.IsCreated) && parameters && material) {
                command = new GenerationCommand(voxelsAsset ? voxelsAsset.voxels : command.voxels, parameters);
                VoxelRenderer.GetRenderer(this).generator.Schedule(command, parameters.jobHorizontalSize, parameters.asynchronousGeneration, AddToLayer);
            }
        }

        private void AddToLayer(GenerationCommand command) {
            if (this && layer == null && command.Equals(this.command)) {
                VoxelRenderer renderer = VoxelRenderer.GetRenderer(this);
                layer = renderer.GetLayer(this);
                renderer.meshBuffers.AddReference(command);
                if (gameObject.activeSelf) layer.AddInstance(this);
            }
        }

        private void RemoveFromLayer() {
            if (layer == null) return;
            if (gameObject.activeSelf) layer.RemoveInstance(this);
            VoxelRenderer.GetRenderer(this).meshBuffers.RemoveReference(command);
            layer = null;
        }


        /// <summary>
        /// Complete the generation of this object's mesh
        /// </summary>
        public void CompleteGeneration() => VoxelRenderer.GetRenderer(this).generator.Complete(command);
    }

}