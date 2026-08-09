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
        public bool generated { get; internal set; }


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
            if (generated) {
                layer = VoxelRenderer.GetRenderer(this).GetLayer(this);
                layer.AddInstance(this);
            }
        }

        private void OnDisable() {
            if (layer != null && layer.layerBuffers.IsCreated) {
                layer.RemoveInstance(this);
                layer = null;
            }
        }

        private void OnDestroy() {
            if (layer != null && layer.layerBuffers.IsCreated) RemoveFromLayer();
        }


        internal void Generate() {
            if ((voxelsAsset || command.voxels.IsCreated) && parameters && material) {
                command = new GenerationCommand(voxelsAsset ? voxelsAsset.voxels : command.voxels, parameters);
                VoxelRenderer.GetRenderer(this).generator.Schedule(command, parameters.jobHorizontalSize, parameters.asynchronousGeneration, AddToLayer);
            }
        }

        private void AddToLayer(GenerationCommand command) {
            if (this && isActiveAndEnabled && layer == null && command.Equals(this.command)) {
                VoxelRenderer renderer = VoxelRenderer.GetRenderer(this);
                layer = renderer.GetLayer(this);
                layer.AddInstance(this);
                renderer.meshBuffers.AddReference(command);
                generated = true;
            }
        }

        private void RemoveFromLayer() {
            if (layer != null) {
                layer.RemoveInstance(this);
                layer = null;
                VoxelRenderer.GetRenderer(this).meshBuffers.RemoveReference(command);
                generated = false;
            }
        }


        /// <summary>
        /// Complete the generation of this object's mesh
        /// </summary>
        public void CompleteGeneration() => VoxelRenderer.GetRenderer(this).generator.Complete(command);
    }

}