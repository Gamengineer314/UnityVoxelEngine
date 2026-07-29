using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace Voxels.Rendering {

    /// <summary>
    /// Rendering layer that contains all meshes in a layer with a material
    /// </summary>
    internal class VoxelLayer {
        public readonly int layer;
        public readonly ShaderParameters parameters;
        public readonly LayerBuffers layerBuffers;
        private readonly MeshBuffers meshBuffers;

        private readonly List<List<VoxelMesh>> instances = new();
        private readonly Dictionary<VoxelMesh, int> instanceIndices = new();
        private readonly Dictionary<GenerationCommand, int> meshIndices = new();
        
        private static readonly Dictionary<Material, VoxelLayer[]> layers = new();


        public VoxelLayer(int layer, ShaderParameters parameters) {
            this.layer = layer;
            this.parameters = parameters;
            layerBuffers = new LayerBuffers(parameters);
            meshBuffers = VoxelRenderer.Instance.meshBuffers;
            if (!parameters.instance) instances.Add(new List<VoxelMesh>());
            meshBuffers.bufferCompacted += UpdateChunks;
        }

        public void Dispose() {
            layerBuffers.Dispose();
            meshBuffers.bufferCompacted -= UpdateChunks;
        }


        /// <summary>
        /// Get the rendering layer for a layer and a material
        /// </summary>
        /// <param name="layer">The layer</param>
        /// <param name="material">The material</param>
        /// <returns>The rendering layer</returns>
        public static VoxelLayer GetLayer(int layer, Material material) {
            if (!layers.TryGetValue(material, out VoxelLayer[] materialLayers)) {
                materialLayers = new VoxelLayer[32];
                layers[material] = materialLayers;
                material.SetFloat(ShaderID.quadsInterleaving, VoxelRenderer.Instance.QuadsInterleaving);
            }
            if (materialLayers[layer] == null) {
                ShaderParameters parameters = new(material);
                materialLayers[layer] = new VoxelLayer(layer, parameters);
            }
            return materialLayers[layer];
        }

        /// <summary>
        /// Get the non-empty rendering layers for all layers in a layer mask and all materials
        /// </summary>
        /// <param name="layerMask">The layer mask</param>
        /// <returns>Iterable of (layer, material, rendering layer) tuples</returns>
        public static IEnumerable<(int, Material, VoxelLayer)> GetLayers(int layerMask) {
            foreach (KeyValuePair<Material, VoxelLayer[]> kv in layers) {
                for (int layer = 0; layer < 32; layer++) {
                    if ((layerMask & (1 << layer)) != 0 && kv.Value[layer] != null && kv.Value[layer].layerBuffers.ChunkCount != 0) {
                        yield return (layer, kv.Key, kv.Value[layer]);
                    }
                }
            }
        }

        /// <summary>
        /// All materials used by voxel meshes
        /// </summary>
        public static IEnumerable<Material> Materials => layers.Keys;

        /// <summary>
        /// All rendering layers
        /// </summary>
        public static IEnumerable<VoxelLayer> AllLayers {
            get {
                foreach (KeyValuePair<Material, VoxelLayer[]> kv in layers) {
                    for (int layer = 0; layer < 32; layer++) {
                        if (kv.Value[layer] != null) yield return kv.Value[layer];
                    }
                }
            }
        }

        /// <summary>
        /// Dispose all rendering layers
        /// </summary>
        public static void DisposeAll() {
            foreach (VoxelLayer layer in AllLayers) {
                layer.Dispose();
            }
            layers.Clear();
        }


        /// <summary>
        /// Update layer and transform of the objects in this layer
        /// </summary>
        public void Update() {
            for (int i = instances.Count - 1; i >= 0; i--) {
                List<VoxelMesh> meshInstances = instances[i];
                for (int j = meshInstances.Count - 1; j >= 0; j--) {
                    VoxelMesh instance = meshInstances[j];
                    if (instance.gameObject.layer != layer) {
                        RemoveInstance(instance);
                        instance.layer = GetLayer(instance.gameObject.layer, instance.material);
                        instance.layer.AddInstance(instance);
                    }
                    else if (parameters.transform) layerBuffers.UpdateTransform(i, j, instance.transform.localToWorldMatrix);
                }
            }
        }


        /// <summary>
        /// Add an instance of a mesh to this layer
        /// </summary>
        /// <param name="instance">The instance</param>
        public void AddInstance(VoxelMesh instance) {
            int instanceIndex;
            if (parameters.instance && meshIndices.TryGetValue(instance.command, out int meshIndex)) { // Add instance to existing mesh
                instanceIndex = instances[meshIndex].Count;
                layerBuffers.SetInstances(meshIndex, instances[meshIndex].Count + 1, meshBuffers.GetChunks(instance.command).Length);
            }
            else { // Add mesh with 1 instance
                layerBuffers.AddMesh(meshBuffers, instance.command);
                if (parameters.instance) {
                    meshIndex = instances.Count;
                    instanceIndex = 0;
                    meshIndices[instance.command] = meshIndex;
                    instances.Add(new List<VoxelMesh>());
                }
                else {
                    meshIndex = 0;
                    instanceIndex = instances[0].Count;
                }
            }
            instanceIndices[instance] = instanceIndex;
            instances[meshIndex].Add(instance);
            if (parameters.transform) layerBuffers.UpdateTransform(meshIndex, instanceIndex, instance.transform.localToWorldMatrix);
        }


        /// <summary>
        /// Remove an instance of a mesh from this layer
        /// </summary>
        /// <param name="instance">The instance</param>
        public void RemoveInstance(VoxelMesh instance) {
            int meshIndex = parameters.instance ? meshIndices[instance.command] : 0;
            int instanceIndex = instanceIndices[instance];
            instanceIndices.Remove(instance);

            instances[meshIndex].RemoveAtSwapBack(instanceIndex);
            if (parameters.transform && instances[meshIndex].Count > instanceIndex) {
                layerBuffers.UpdateTransform(meshIndex, instanceIndex, instances[meshIndex][instanceIndex].transform.localToWorldMatrix);
            }
            if (parameters.instance) {
                if (instances[meshIndex].Count == 0) { // Remove mesh
                    instances.RemoveAtSwapBack(meshIndex);
                    meshIndices.Remove(instance.command);
                    layerBuffers.RemoveMesh(meshIndex);
                }
                else { // Remove instance
                    layerBuffers.SetInstances(meshIndex, instances[meshIndex].Count, meshBuffers.GetChunks(instance.command).Length);
                }
            }
            else { // Remove mesh
                layerBuffers.RemoveMesh(instanceIndex);
            }
        }


        private void UpdateChunks() {
            if (parameters.instance) {
                for (int i = 0; i < instances.Count; i++) {
                    layerBuffers.UpdateChunks(meshBuffers, instances[i][0].command, i);
                }
            }
            else {
                for (int i = 0; i < instances[0].Count; i++) {
                    layerBuffers.UpdateChunks(meshBuffers, instances[0][i].command, i);
                }
            }
            layerBuffers.SynchronizeChunks();
        }
    }

}