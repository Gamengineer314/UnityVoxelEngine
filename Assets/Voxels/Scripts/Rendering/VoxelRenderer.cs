using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace Voxels.Rendering {

    /// <summary>
    /// Global voxel renderer
    /// </summary>
    [ExecuteInEditMode]
    public class VoxelRenderer : MonoBehaviour {
        internal const int maxFaceCount = 16384;

        internal static VoxelRenderer sceneRenderer; // Current scene renderer

        [SerializeField] private ComputeShader cullingShader;
        [SerializeField] private float quadsInterleaving = 0.05f; // Remove 1 pixel gaps between triangles

        internal GraphicsBuffer indicesBuffer { get; private set; } // All 16 bits indices
        internal GraphicsBuffer counterBuffer { get; private set; } // Buffer to store a counter
        internal MeshBuffers meshBuffers { get; private set; } // Global mesh buffers
        internal MeshGenerator generator { get; private set; }
        private readonly Dictionary<Camera, CameraRenderer> renderers = new();
        private readonly Dictionary<(Material, ShaderParameters), VoxelLayer[]> layers = new();

#if UNITY_EDITOR
        internal static Dictionary<Scene, VoxelRenderer> previewRenderers = new(); // Prefab and asset preview renderers
        internal Material wireframeMaterial;
#endif

        public float QuadsInterleaving {
            get => quadsInterleaving;
            set {
                quadsInterleaving = value;
                foreach (Material material in Materials) {
                    material.SetFloat(ShaderID.quadsInterleaving, quadsInterleaving);
                }
            }
        }

        public ComputeShader CullingShader {
            get => cullingShader;
            set {
                cullingShader = value;
                ShaderID.SetKeywords(cullingShader);
            }
        }


        /// <summary>
        /// All rendering layers
        /// </summary>
        internal IEnumerable<VoxelLayer> Layers {
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
        internal IEnumerable<Material> Materials => layers.Keys.Select(k => k.Item1);


        /// <summary>
        /// Get the renderer for an instance
        /// </summary>
        /// <param name="instance">The instance</param>
        /// <returns>The renderer, or null if it shouldn't be rendered</returns>
        internal static VoxelRenderer GetRenderer(VoxelMesh instance) {
#if UNITY_EDITOR
            Scene scene = instance.gameObject.scene;
            if (scene == SceneManager.GetActiveScene()) return sceneRenderer;
            else {
                if (previewRenderers.TryGetValue(scene, out VoxelRenderer renderer)) return renderer;
                GameObject rendererObject = new("Voxel Renderer") { hideFlags = HideFlags.HideAndDontSave };
                SceneManager.MoveGameObjectToScene(rendererObject, scene);
                renderer = rendererObject.AddComponent<VoxelRenderer>();
                return renderer;
            }
#else
            return sceneRenderer;
#endif            
        }
        
        
        /// <summary>
        /// Get the rendering layer for an instance
        /// </summary>
        /// <param name="instance">The instance</param>
        /// <returns>The rendering layer</returns>
        internal VoxelLayer GetLayer(VoxelMesh instance) {
            Material material = instance.material;
            ShaderParameters parameters = new(instance.parameters.textured, instance.parameters.instanced);
            int layer = instance.gameObject.layer;
            if (!layers.TryGetValue((material, parameters), out VoxelLayer[] materialLayers)) {
                materialLayers = new VoxelLayer[32];
                layers[(material, parameters)] = materialLayers;
                material.SetFloat(ShaderID.quadsInterleaving, quadsInterleaving);
            }
            if (materialLayers[layer] == null) {
                materialLayers[layer] = new VoxelLayer(layer, material, parameters, meshBuffers);
            }
            return materialLayers[layer];
        }


        /// <summary>
        /// Get the non-empty rendering layers for all layers in a layer mask and all materials
        /// </summary>
        /// <param name="layerMask">The layer mask</param>
        /// <returns>Enumerable of rendering layers</returns>
        internal IEnumerable<VoxelLayer> GetLayers(int layerMask) {
            foreach (KeyValuePair<(Material, ShaderParameters), VoxelLayer[]> kv in layers) {
                for (int layer = 0; layer < 32; layer++) {
                    if ((layerMask & (1 << layer)) != 0 && kv.Value[layer] != null && kv.Value[layer].layerBuffers.ChunkCount != 0) {
                        yield return kv.Value[layer];
                    }
                }
            }
        }


        private void OnValidate() {
            CullingShader = cullingShader;
            QuadsInterleaving = quadsInterleaving;
        }


        internal void Awake() {
#if UNITY_EDITOR
            if (gameObject.scene == SceneManager.GetActiveScene()) {
                if (sceneRenderer) throw new InvalidOperationException("Can't create more than one VoxelRenderer in a scene");
                sceneRenderer = this;
            }
            else {
                if (previewRenderers.ContainsKey(gameObject.scene)) throw new InvalidOperationException("Can't create more than one VoxelRenderer in a scene");
                previewRenderers[gameObject.scene] = this;
            }
            if ((!sceneRenderer || sceneRenderer == this) && (previewRenderers.Count == 0 || previewRenderers.Count == 1 && previewRenderers.Values.First() == this)) {
                Camera.onPreCull += RenderSwitch;
            }
#else
            if (sceneRenderer) throw new InvalidOperationException("Can't create more than one VoxelRenderer");
            sceneRenderer = this;
            Camera.onPreCull += Render;
#endif

            ushort[] indices = new ushort[maxFaceCount * 6];
            for (int i = 0; i < maxFaceCount; i++) {
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
            meshBuffers = new MeshBuffers();
            generator = new MeshGenerator(meshBuffers);
            if (cullingShader != null) CullingShader = cullingShader;
#if UNITY_EDITOR
            wireframeMaterial = AssetDatabase.LoadAssetAtPath<Material>(Path.Combine("Assets", "Voxels", "Shaders", "Wireframe.mat"));
#endif
        }


        internal void OnDestroy() {
#if UNITY_EDITOR
            if (!meshBuffers.IsCreated) return; // Avoid destroying twice when the renderer is destroyed after beforeAssemblyReload
            if (sceneRenderer == this) sceneRenderer = null;
            else previewRenderers.Remove(gameObject.scene);
            if (!sceneRenderer && previewRenderers.Count == 0) Camera.onPreCull -= RenderSwitch;
#else
            sceneRenderer = null;
            Camera.onPreCull -= Render;
#endif

            indicesBuffer.Dispose();
            counterBuffer.Dispose();
            meshBuffers.Dispose();
            generator.Dispose();
            foreach (CameraRenderer renderer in renderers.Values) renderer.Dispose();
            foreach (VoxelLayer layer in Layers) {
                layer.Dispose();
            }
            layers.Clear();
        }


        private void LateUpdate() {
            generator.Update();
            foreach (VoxelLayer layer in Layers) {
                layer.LateUpdate();
            }
        }


        private void Render(Camera camera) {
            if (!renderers.TryGetValue(camera, out CameraRenderer renderer)) {
                renderer = new CameraRenderer(camera);
                renderers[camera] = renderer;
            }
            renderer.Render(this);
        }

#if UNITY_EDITOR
        private static void RenderSwitch(Camera camera) {
            if (camera.cameraType == CameraType.Game) {
                if (sceneRenderer) sceneRenderer.Render(camera);
            }
            else if (!camera.gameObject.scene.IsValid()) { // Scene camera or camera preview
                PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
                if (stage) { // Prefab scene
                    if (previewRenderers.TryGetValue(stage.scene, out VoxelRenderer renderer)) {
                        renderer.Render(camera);
                    }
                }
                else { // Main scene
                    if (sceneRenderer) sceneRenderer.Render(camera);
                }
            }
            else { // Asset preview camera
                if (previewRenderers.TryGetValue(camera.gameObject.scene, out VoxelRenderer renderer)) {
                    renderer.Render(camera);   
                }
            }
        }


        private void OnRenderObject() {
            if (Camera.current.cameraType == CameraType.SceneView) {
                DrawCameraMode mode = SceneView.currentDrawingSceneView.cameraMode.drawMode;
                if (mode == DrawCameraMode.Wireframe || mode == DrawCameraMode.TexturedWire) {
                    RenderWireframe();   
                }
            }
        }


        /// <summary>
        /// Render the scene in wireframe mode
        /// </summary>
        private void RenderWireframe() {
            GL.wireframe = true;
            wireframeMaterial.SetBuffer(ShaderID.faces, meshBuffers.facesBuffer);
            wireframeMaterial.SetBuffer(ShaderID.colors, meshBuffers.colorsBuffer);
            renderers[Camera.current].RenderWireframe(this);
            GL.wireframe = false;
        }
#endif
    }

}