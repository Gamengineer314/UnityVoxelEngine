using System;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
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
                command = new GenerationCommand(value, parameters, material);
                Generate();
            }
        }

        public GenerationParameters Parameters {
            get => parameters;
            set {
                RemoveFromLayer();
                parameters = value;
                command = new GenerationCommand(command.voxels, parameters, material);
                Generate();
            }
        }

        public Material Material {
            get => material;
            set {
                RemoveFromLayer();
                material = value;
                command = new GenerationCommand(command.voxels, parameters, material);
                Generate();
            }
        }

        private void OnValidate() {
            RemoveFromLayer();
            if ((voxelsAsset && voxelsAsset.voxels.IsCreated || command.voxels.IsCreated) && parameters && parameters.chunkSize != 0 && material && VoxelRenderer.Instance) {
                if (voxelsAsset && voxelsAsset.voxels.IsCreated) command = new GenerationCommand(voxelsAsset.voxels, parameters, material);
                Generate();
            }
            else command = default;
        }


        internal void Start() {
            if (voxelsAsset && !command.voxels.IsCreated) {
                command = new GenerationCommand(voxelsAsset.voxels, parameters, material);
                Generate();
            }
        }

        private void OnEnable() {
            if (layer != null) {
                layer = VoxelLayer.GetLayer(gameObject.layer, material);
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
            VoxelRenderer.Instance.generator.Schedule(command, parameters.jobHorizontalSize, parameters.asynchronousGeneration, AddToLayer);
        }

        private void AddToLayer(GenerationCommand command) {
            if (this && layer == null && command.Equals(this.command)) {
                layer = VoxelLayer.GetLayer(gameObject.layer, material);
                if (gameObject.activeSelf) layer.AddInstance(this);
                VoxelRenderer.Instance.meshBuffers.AddReference(command);
            }
        }

        private void RemoveFromLayer() {
            if (layer == null) return;
            if (gameObject.activeSelf) layer.RemoveInstance(this);
            VoxelRenderer.Instance.meshBuffers.RemoveReference(command);
            layer = null;
        }


        /// <summary>
        /// Complete the generation of this object's mesh
        /// </summary>
        public void CompleteGeneration() => VoxelRenderer.Instance.generator.Complete(command);
    }

}