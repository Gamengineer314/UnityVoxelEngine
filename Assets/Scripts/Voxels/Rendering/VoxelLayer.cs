using System.Collections.Generic;
using Unity.Collections;
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
        
        private static readonly Dictionary<Material, VoxelLayer[]> layers = new();


        public VoxelLayer(ShaderParameters parameters) {
            this.parameters = parameters;
            layerBuffers = new LayerBuffers(parameters);
            meshBuffers = VoxelRenderer.Instance.meshBuffers;
            generator = new MeshGenerator(meshBuffers);
            if (!parameters.instance) instances.Add(new List<VoxelMesh>());
        }

        public void Dispose() {
            generator.Dispose();
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
                    if ((layerMask & (1 << layer)) != 0 && kv.Value[layer] != null && kv.Value[layer].layerBuffers.chunks.Length != 0) {
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
                    int startChunk = layerBuffers.chunks.Length;
                    foreach (VoxelChunk chunk in meshBuffers.GetChunks(meshes[i].command)) {
                        layerBuffers.chunks.Add(new VoxelChunk(
                            chunk.center, chunk.size, chunk.offset.position, chunk.offset.Color,
                            chunk.Normal, chunk.StartFace, chunk.FaceCount, i, 0, 0
                        ));
                    }
                    layerBuffers.SynchronizeChunks(startChunk, layerBuffers.chunks.Length - startChunk);
                }
            }

            // Update transforms
            for (int i = 0; i < instances.Count; i++) {
                for (int j = 0; j < instances[i].Count; j++) {
                    VoxelMesh instance = instances[i][j];
                    if (parameters.transform) {
                        Matrix4x4 transform = instance.transform.localToWorldMatrix;
                        if (transform != layerBuffers.transforms[j]) {
                            layerBuffers.transforms[j] = transform;
                            layerBuffers.SynchronizeTransforms(i, 1);
                        }
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
            meshes.Add((command, false));
            generator.Schedule(command, mesh.parameters.jobHorizontalSize);
            instances[0].Add(mesh);
            if (parameters.transform) {
                Matrix4x4 transform = mesh.transform.localToWorldMatrix;
                layerBuffers.transforms.Add(transform);
                layerBuffers.SynchronizeTransforms(layerBuffers.transforms.Length - 1, 1);
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