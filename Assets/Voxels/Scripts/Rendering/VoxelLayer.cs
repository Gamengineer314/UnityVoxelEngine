using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using UnityEngine;

namespace Voxels.Rendering {

    /// <summary>
    /// Rendering layer that contains all meshes in a layer with a material
    /// </summary>
    internal class VoxelLayer {
        public readonly int layer;
        public readonly Material material;
        public readonly ShaderParameters parameters;
        public readonly LayerBuffers layerBuffers;
        private readonly MeshBuffers meshBuffers;

        private readonly List<List<VoxelMesh>> instances = new();
        private readonly Dictionary<VoxelMesh, int> instanceIndices = new();
        private readonly Dictionary<GenerationCommand, int> meshIndices = new();
        
        private static readonly Dictionary<(Material, ShaderParameters), VoxelLayer[]> layers = new();


        public VoxelLayer(int layer, Material material, ShaderParameters parameters) {
            this.layer = layer;
            this.material = material;
            this.parameters = parameters;
            layerBuffers = new LayerBuffers(parameters);
            meshBuffers = VoxelRenderer.Instance.meshBuffers;
            if (!parameters.instanced) instances.Add(new List<VoxelMesh>());
            meshBuffers.bufferCompacted += UpdateChunks;
        }

        public void Dispose() {
            layerBuffers.Dispose();
            meshBuffers.bufferCompacted -= UpdateChunks;
        }


        /// <summary>
        /// Get the rendering layer for an instance
        /// </summary>
        /// <param name="instance">The instance</param>
        /// <returns>The rendering layer</returns>
        public static VoxelLayer GetLayer(VoxelMesh instance) {
            Material material = instance.material;
            ShaderParameters parameters = new(instance.parameters.textured, instance.parameters.instanced);
            int layer = instance.gameObject.layer;
            if (!layers.TryGetValue((material, parameters), out VoxelLayer[] materialLayers)) {
                materialLayers = new VoxelLayer[32];
                layers[(material, parameters)] = materialLayers;
                material.SetFloat(ShaderID.quadsInterleaving, VoxelRenderer.Instance.QuadsInterleaving);
            }
            if (materialLayers[layer] == null) {
                materialLayers[layer] = new VoxelLayer(layer, material, parameters);
            }
            return materialLayers[layer];
        }

        /// <summary>
        /// Get the non-empty rendering layers for all layers in a layer mask and all materials
        /// </summary>
        /// <param name="layerMask">The layer mask</param>
        /// <returns>Enumerable of rendering layers</returns>
        public static IEnumerable<VoxelLayer> GetLayers(int layerMask) {
            foreach (KeyValuePair<(Material, ShaderParameters), VoxelLayer[]> kv in layers) {
                for (int layer = 0; layer < 32; layer++) {
                    if ((layerMask & (1 << layer)) != 0 && kv.Value[layer] != null && kv.Value[layer].layerBuffers.ChunkCount != 0) {
                        yield return kv.Value[layer];
                    }
                }
            }
        }

        /// <summary>
        /// All rendering layers
        /// </summary>
        public static IEnumerable<VoxelLayer> Layers {
            get {
                foreach (KeyValuePair<(Material, ShaderParameters), VoxelLayer[]> kv in layers) {
                    for (int layer = 0; layer < 32; layer++) {
                        if (kv.Value[layer] != null) yield return kv.Value[layer];
                    }
                }
            }
        }

        /// <summary>
        /// All materials used by voxel meshes
        /// </summary>
        public static IEnumerable<Material> Materials => layers.Keys.Select(k => k.Item1);

        /// <summary>
        /// Dispose all rendering layers
        /// </summary>
        public static void DisposeAll() {
            foreach (VoxelLayer layer in Layers) {
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
                        instance.layer = GetLayer(instance);
                        instance.layer.AddInstance(instance);
                    }
                    else layerBuffers.UpdateTransform(i, j, instance.transform.localToWorldMatrix);
                }
            }
        }


        /// <summary>
        /// Add an instance of a mesh to this layer
        /// </summary>
        /// <param name="instance">The instance</param>
        public void AddInstance(VoxelMesh instance) {
            int instanceIndex;
            if (parameters.instanced && meshIndices.TryGetValue(instance.command, out int meshIndex)) { // Add instance to existing mesh
                instanceIndex = instances[meshIndex].Count;
                layerBuffers.SetInstances(meshIndex, instances[meshIndex].Count + 1, meshBuffers.GetChunks(instance.command).Length);
            }
            else { // Add mesh with 1 instance
                layerBuffers.AddMesh(meshBuffers, instance.command);
                if (parameters.instanced) {
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
            layerBuffers.UpdateTransform(meshIndex, instanceIndex, instance.transform.localToWorldMatrix);
        }


        /// <summary>
        /// Remove an instance of a mesh from this layer
        /// </summary>
        /// <param name="instance">The instance</param>
        public void RemoveInstance(VoxelMesh instance) {
            int meshIndex = parameters.instanced ? meshIndices[instance.command] : 0;
            int instanceIndex = instanceIndices[instance];
            instanceIndices.Remove(instance);

            instances[meshIndex].RemoveAtSwapBack(instanceIndex);
            if (instances[meshIndex].Count > instanceIndex) {
                layerBuffers.UpdateTransform(meshIndex, instanceIndex, instances[meshIndex][instanceIndex].transform.localToWorldMatrix);
            }
            if (parameters.instanced) {
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
            if (parameters.instanced) {
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