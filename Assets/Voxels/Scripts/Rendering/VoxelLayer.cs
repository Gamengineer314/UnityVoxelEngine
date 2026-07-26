using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Voxels.Rendering {

    /// <summary>
    /// Rendering layer that contains all meshes in a layer with a material
    /// </summary>
    internal class VoxelLayer {
        public readonly ShaderParameters parameters;
        public readonly LayerBuffers layerBuffers;
        private readonly MeshBuffers meshBuffers;
        private readonly MeshGenerator generator;

        private readonly List<(GenerationCommand command, bool generated)> meshes = new();
        private readonly List<List<VoxelMesh>> instances = new();
        private readonly Dictionary<GenerationCommand, int> meshIndices = new();
        
        private static readonly Dictionary<Material, VoxelLayer[]> layers = new();


        public VoxelLayer(ShaderParameters parameters) {
            this.parameters = parameters;
            layerBuffers = new LayerBuffers(parameters);
            meshBuffers = VoxelRenderer.Instance.meshBuffers;
            generator = VoxelRenderer.Instance.generator;
            if (!parameters.instance) instances.Add(new List<VoxelMesh>());
        }

        public void Dispose() {
            layerBuffers.Dispose();
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
                materialLayers[layer] = new VoxelLayer(parameters);
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
        /// Update generation and transform of the objects in this layer
        /// </summary>
        public void Update() {
            // Add chunks for completed meshes
            for (int i = 0; i < meshes.Count; i++) {
                if (!meshes[i].generated && generator.CompleteCompleted(meshes[i].command)) {
                    meshes[i] = (meshes[i].command, true);
                    NativeList<VoxelChunk> chunks = meshBuffers.GetChunks(meshes[i].command);
                    if (parameters.instance) layerBuffers.SetInstances(i, instances[i].Count, chunks.Length);
                    layerBuffers.AddChunks(i, chunks, parameters.instance ? instances[i].Count : 0);
                }
            }

            // Update transforms
            if (parameters.transform) {
                for (int i = 0; i < instances.Count; i++) {
                    for (int j = 0; j < instances[i].Count; j++) {
                        layerBuffers.UpdateTransform(i, j, instances[i][j].transform.localToWorldMatrix);
                    }
                }
            }
        }


        /// <summary>
        /// Add an instance of a mesh to this layer
        /// </summary>
        /// <param name="mesh">The mesh</param>
        public void AddObject(VoxelMesh mesh) {
            GenerationCommand command = GetCommand(mesh);
            if (parameters.instance && meshIndices.TryGetValue(command, out int index)) {
                instances[index].Add(mesh);
                layerBuffers.SetInstances(index, instances[index].Count, meshBuffers.GetChunks(command).Length);
                layerBuffers.UpdateTransform(index, instances[index].Count - 1, mesh.transform.localToWorldMatrix);
            }
            else {
                meshes.Add((command, false));
                generator.Schedule(command, mesh.parameters.jobHorizontalSize);
                if (parameters.instance) {
                    meshIndices[command] = instances.Count;
                    instances.Add(new List<VoxelMesh> { mesh });
                }
                else instances[0].Add(mesh);
                layerBuffers.AddMesh();
            }
        }


        /// <summary>
        /// Complete generation of a mesh
        /// <param name="mesh">The mesh</param>
        /// </summary>
        public void CompleteGeneration(VoxelMesh mesh)
            => generator.Complete(GetCommand(mesh));


        private GenerationCommand GetCommand(VoxelMesh mesh)
            => new(mesh.voxels, mesh.parameters.chunkSize, mesh.parameters.mergeNormalsThreshold, mesh.parameters.seenFromAbove, parameters.texture);
    }

}