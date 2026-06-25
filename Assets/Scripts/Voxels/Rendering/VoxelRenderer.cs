using System;
using System.Collections.Generic;
using UnityEngine;

namespace Voxels.Rendering {

    /// <summary>
    /// Global voxel renderer
    /// </summary>
    [ExecuteInEditMode]
    public class VoxelRenderer : MonoBehaviour {
        internal const int maxFaceCount = 16384;

        internal static VoxelRenderer Instance { get; private set; }

        [SerializeField] internal ComputeShader cullingShader;
        [SerializeField] internal GenerationParameters[] generationParameters;
        [SerializeField] private float quadsInterleaving = 0.05f; // Remove 1 pixel gaps between triangles

        public float QuadsInterleaving {
            get => quadsInterleaving;
            set {
                quadsInterleaving = value;
                foreach (Material material in VoxelLayer.Materials) {
                    material.SetFloat(ShaderID.quadsInterleaving, quadsInterleaving);
                }
            }
        }

        internal GraphicsBuffer indicesBuffer { get; private set; } // All 16 bits indices
        internal GraphicsBuffer counterBuffer { get; private set; } // Buffer to store a counter
        private readonly Dictionary<Camera, CameraRenderer> renderers = new();


        private void Reset() {
            generationParameters = new GenerationParameters[32];
            for (int i = 0; i < 32; i++) {
                generationParameters[i] = GenerationParameters.Default;
            }
        }

        private void OnValidate() {
            ShaderID.SetKeywords(cullingShader);            
        }


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

            if (cullingShader != null) ShaderID.SetKeywords(cullingShader);
            Camera.onPreCull += Render;
        }


        internal void OnDestroy() {
            Instance = null;
            indicesBuffer.Dispose();
            counterBuffer.Dispose();
            foreach (CameraRenderer renderer in renderers.Values) renderer.Dispose();
            VoxelLayer.DisposeAll();
            Camera.onPreCull -= Render;
        }


        private void Update() {
            foreach (VoxelLayer layer in VoxelLayer.AllLayers) {
                layer.Update();
            }
        }

        private void Render(Camera camera) {
            if (!renderers.TryGetValue(camera, out CameraRenderer renderer)) {
                renderer = new CameraRenderer(camera);
                renderers[camera] = renderer;
            }
            renderer.Render();
        }
    }

}