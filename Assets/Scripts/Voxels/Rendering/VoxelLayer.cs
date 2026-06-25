using System.Collections.Generic;
using UnityEngine;

namespace Voxels.Rendering {

    /// <summary>
    /// Rendering layer that contains all meshes in a layer with a material
    /// </summary>
    internal class VoxelLayer {
        public readonly ShaderParameters shaderParams;
        public readonly LayerData data;

        private readonly MeshGenerator generator;
        private readonly List<(VoxelMesh mesh, bool generated)> meshes = new();
        private readonly Dictionary<VoxelMesh, int> indices = new(); // Index of each mesh in [meshes]
        

        private static readonly Dictionary<Material, VoxelLayer[]> layers = new();

        public VoxelLayer(ShaderParameters shaderParams, GenerationParameters generationParams) {
            this.shaderParams = shaderParams;
            data = new LayerData(shaderParams);
            generator = new MeshGenerator(data, shaderParams.texture, generationParams);
        }

        public void Dispose() {
            generator.Dispose();
            data.Dispose();
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
                materialLayers[layer] = new VoxelLayer(new ShaderParameters(material), VoxelRenderer.Instance.generationParameters[layer]);
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
                    if ((layerMask & (1 << layer)) != 0 && kv.Value[layer] != null && kv.Value[layer].data.cpu.chunks.Length != 0) {
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
            for (int i = 0; i < meshes.Count; i++) {
                VoxelMesh mesh = meshes[i].mesh;
                if (!meshes[i].generated) { // Update generation
                    if (generator.CompleteCompleted(mesh.voxels, i)) {
                        meshes[i] = (mesh, true);
                    }
                }
                if (shaderParams.transform) { // Update transform 
                    Matrix4x4 transform = mesh.transform.localToWorldMatrix;
                    if (transform != data.cpu.transforms[i]) {
                        data.cpu.transforms[i] = transform;
                        data.gpu.transforms[i] = transform;
                    }
                }
            }
        }


        /// <summary>
        /// Add an instance of a mesh to this layer
        /// </summary>
        /// <param name="mesh">The mesh</param>
        public void AddObject(VoxelMesh mesh) {
            indices[mesh] = meshes.Count;
            meshes.Add((mesh, false));
            if (shaderParams.transform) {
                Matrix4x4 transform = mesh.transform.localToWorldMatrix;
                data.cpu.transforms.Add(transform);
                data.gpu.transforms.Add(transform);
            }
            generator.Schedule(mesh.voxels, mesh.offset);
        }


        /// <summary>
        /// Complete generation of a mesh
        /// <param name="mesh">The mesh</param>
        /// </summary>
        public void CompleteGeneration(VoxelMesh mesh) {
            int i = indices[mesh];
            if (!meshes[i].generated) {
                generator.Complete(mesh.voxels, i);
                meshes[i] = (mesh, true);
            }
        }
    }

}