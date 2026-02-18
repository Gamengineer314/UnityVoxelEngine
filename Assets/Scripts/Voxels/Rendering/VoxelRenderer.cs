using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Voxels.Rendering {

    /// <summary>
    /// Global voxel renderer
    /// </summary>
    [ExecuteInEditMode]
    public class VoxelRenderer : MonoBehaviour {
        internal const int maxFaceCount = 16384;

        internal static VoxelRenderer Instance { get; private set; }

        [SerializeField] internal Material terrainMaterial;
        [SerializeField] internal Material objectsMaterial;
        [SerializeField] internal ComputeShader terrainCulling;
        [SerializeField] internal ComputeShader objectsCulling;
        [SerializeField] private float quadsInterleaving = 0.05f; // Remove 1 pixel gaps between triangles

        public float QuadsInterleaving {
            get => quadsInterleaving;
            set {
                quadsInterleaving = value;
                if (terrainMaterial) terrainMaterial.SetFloat(ShaderID.quadsInterleaving, quadsInterleaving);
                if (objectsMaterial) objectsMaterial.SetFloat(ShaderID.quadsInterleaving, quadsInterleaving);
            }
        }

        internal GraphicsBuffer indicesBuffer { get; private set; } // All 16 bits indices
        internal GraphicsBuffer counterBuffer { get; private set; } // Buffer to store a counter
        private readonly Dictionary<Camera, Renderer> renderers = new();


        internal void Awake() {
            if (Instance) throw new InvalidOperationException("Can't create more than one VoxelRenderer");
            Instance = this;

            ushort[] indices = new ushort[98304];
            for (int i = 0; i < 16384; i++) {
                indices[6 * i] = (ushort)(4 * i);
                indices[6 * i + 1] = (ushort)(4 * i + 1);
                indices[6 * i + 2] = (ushort)(4 * i + 2);
                indices[6 * i + 3] = (ushort)(4 * i + 2);
                indices[6 * i + 4] = (ushort)(4 * i + 1);
                indices[6 * i + 5] = (ushort)(4 * i + 3);
            }
            indicesBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Index, indices.Length, sizeof(ushort));
            indicesBuffer.SetData(indices);
            counterBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Raw, 1, sizeof(uint));

            QuadsInterleaving = quadsInterleaving;
            terrainMaterial.SetFloat("seed", Random.value);
            Camera.onPreCull += Render;
        }


        internal void OnDestroy() {
            Instance = null;
            indicesBuffer.Dispose();
            counterBuffer.Dispose();
            foreach (Renderer renderer in renderers.Values) renderer.Dispose();
            Camera.onPreCull -= Render;
        }


        private void Render(Camera camera) {
            Renderer renderer = renderers.GetValueOrDefault(camera);
            if (renderer is null) {
                renderer = new Renderer(camera);
                renderers[camera] = renderer;
            }
            renderer.Render();
        }


        private class Renderer {
            private readonly Camera camera;
            private readonly TerrainLayerRenderer[] terrainRenderers = new TerrainLayerRenderer[32];
            private readonly ObjectLayerRenderer[] objectRenderers = new ObjectLayerRenderer[32];

            public Renderer(Camera camera) {
                this.camera = camera;
            }

            public void Dispose() {
                foreach (TerrainLayerRenderer renderer in terrainRenderers) renderer?.Dispose();
                foreach (ObjectLayerRenderer renderer in objectRenderers) renderer?.Dispose();
            }

            public void Render() {
                for (int layer = 0; layer < 32; layer++) {
                    if ((camera.cullingMask & 1 << layer) != 0) { // Render layers seen by the camera
                        VoxelTerrainLayer terrainLayer = VoxelTerrainLayer.GetLayer(layer);
                        if (terrainLayer && !terrainLayer.IsEmpty) { // Render terrain layers
                            TerrainLayerRenderer renderer = terrainRenderers[layer];
                            if (renderer is null) {
                                renderer = new TerrainLayerRenderer(camera, layer);
                                terrainRenderers[layer] = renderer;
                            }
                            renderer.SetBuffers(terrainLayer);
                            renderer.Cull();
                            renderer.Render();
                        }

                        VoxelObjectLayer objectLayer = VoxelObjectLayer.GetLayer(layer);
                        if (objectLayer && !objectLayer.IsEmpty) { // Render object layers
                            ObjectLayerRenderer renderer = objectRenderers[layer];
                            if (renderer is null) {
                                renderer = new ObjectLayerRenderer(camera, layer);
                                objectRenderers[layer] = renderer;
                            }
                            renderer.SetBuffers(objectLayer);
                            renderer.Cull();
                            renderer.Render();
                        }
                    }
                }
            }
        }
    }

}