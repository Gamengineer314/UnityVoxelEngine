#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using Voxels.Rendering;
using System.Collections.Generic;

namespace Voxels.Editor {

    [InitializeOnLoad]
    internal class EditorVoxelRenderer : EditorWindow {
        private static readonly Dictionary<VoxelTerrain, GraphicsBuffer> commandsBuffers = new();
        private static VoxelRenderers voxels = null;

        static EditorVoxelRenderer() {
            SceneView.duringSceneGui += EditorRender;
            AssemblyReloadEvents.beforeAssemblyReload += Dispose;
        }

        private static void Dispose() {
            foreach (GraphicsBuffer buffer in commandsBuffers.Values) buffer.Dispose();
            VoxelTerrain[] terrains = FindObjectsOfType<VoxelTerrain>();
            foreach (VoxelTerrain terrain in terrains) terrain.Dispose();
            if (voxels) voxels.Dispose();
        }


        private static void EditorRender(SceneView view) {
            // Init singletons
            if (!VoxelRenderers.Instance) {
                if (voxels) voxels.Dispose();
                voxels = FindObjectOfType<VoxelRenderers>();
                if (!voxels) return;
                voxels.Init();
            }

            Camera sceneCamera = SceneView.currentDrawingSceneView.camera;
            RenderTerrains(sceneCamera);
        }


        private static void RenderTerrains(Camera sceneCamera) {
            VoxelRenderers voxels = VoxelRenderers.Instance;
            RenderParams renderParams = new(voxels.terrainMaterial) { camera = sceneCamera };

            VoxelTerrain[] terrains = FindObjectsOfType<VoxelTerrain>();
            foreach (VoxelTerrain terrain in terrains) {
                if (!terrain.Created) continue;
                if (!commandsBuffers.ContainsKey(terrain)) commandsBuffers[terrain] = VoxelTerrainRenderer.CreateCommands(terrain.MeshCount);
                GraphicsBuffer commandsBuffer = commandsBuffers[terrain];

                SceneRender render = SceneRender.Get(terrain);
                if (render.mode == SceneRender.Mode.None) continue;
                int count;
                if (render.mode == SceneRender.Mode.All) {
                    count = VoxelTerrainRenderer.PrepareDraw(terrain, sceneCamera, commandsBuffer);
                }
                else {
                    VoxelTerrainRenderer renderer = (VoxelTerrainRenderer)render.renderer;
                    count = VoxelTerrainRenderer.PrepareDraw(terrain, renderer.target, commandsBuffer);
                }

                renderParams.worldBounds = new(Vector3.zero, new Vector3(float.MaxValue, float.MaxValue, float.MaxValue));
                Graphics.RenderPrimitivesIndexedIndirect(renderParams, MeshTopology.Triangles, voxels.indicesBuffer, commandsBuffer, count);
            }
        }
    }

}
#endif