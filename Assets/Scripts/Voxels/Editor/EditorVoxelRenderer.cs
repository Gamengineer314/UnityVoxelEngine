#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using Voxels.Rendering;
using System.Collections.Generic;

namespace Voxels.Editor {

    [InitializeOnLoad]
    internal class EditorVoxelRenderer : EditorWindow {
        private static readonly Dictionary<VoxelTerrain, GraphicsBuffer> commandsBuffers = new();

        static EditorVoxelRenderer() {
            SceneView.duringSceneGui += EditorRender;
            AssemblyReloadEvents.beforeAssemblyReload += Dispose;
        }

        private static void Dispose() {
            foreach (GraphicsBuffer buffer in commandsBuffers.Values) buffer.Dispose();
            VoxelTerrain[] terrains = FindObjectsOfType<VoxelTerrain>();
            foreach (VoxelTerrain terrain in terrains) terrain.Dispose();
            if (VoxelData.Instance) VoxelData.Instance.Dispose();
        }


        private static void EditorRender(SceneView view) {
            // Init singletons
            if (VoxelData.Instance == null && !Application.isPlaying) {
                VoxelData voxels = FindObjectOfType<VoxelData>();
                if (voxels == null) return;
                voxels.Init();
            }

            Camera sceneCamera = SceneView.currentDrawingSceneView.camera;
            RenderTerrains(sceneCamera);
        }


        private static void RenderTerrains(Camera sceneCamera) {
            VoxelData voxels = VoxelData.Instance;
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
                    count = VoxelTerrainRenderer.PrepareDraw(terrain, sceneCamera, terrain.facesBuffer, commandsBuffer);
                }
                else {
                    VoxelTerrainRenderer renderer = (VoxelTerrainRenderer)render.renderer;
                    count = VoxelTerrainRenderer.PrepareDraw(terrain, renderer.target, terrain.facesBuffer, commandsBuffer);
                }

                renderParams.worldBounds = new(Vector3.zero, new Vector3(float.MaxValue, float.MaxValue, float.MaxValue));
                Graphics.RenderPrimitivesIndexedIndirect(renderParams, MeshTopology.Triangles, voxels.indicesBuffer, commandsBuffer, count);
            }
        }
    }

}
#endif