#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using Voxels.Rendering;
using System.Collections.Generic;

namespace Voxels.Editor {

    [InitializeOnLoad]
    internal class EditorVoxelRenderer : EditorWindow {
        private static readonly Dictionary<VoxelTerrain, VoxelTerrainRenderer.Commands> terrainCommands = new();
        private static VoxelRenderers voxels = null;

        static EditorVoxelRenderer() {
            SceneView.duringSceneGui += EditorRender;
            AssemblyReloadEvents.beforeAssemblyReload += Dispose;
        }

        private static void Dispose() {
            foreach (VoxelTerrainRenderer.Commands buffer in terrainCommands.Values) buffer.Dispose();
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
            //RenderTerrains(sceneCamera);
        }


        private static void RenderTerrains(Camera sceneCamera) {
            VoxelRenderers voxels = VoxelRenderers.Instance;

            VoxelTerrain[] terrains = FindObjectsOfType<VoxelTerrain>();
            foreach (VoxelTerrain terrain in terrains) {
                if (!terrain.Created) continue;
                if (!terrainCommands.ContainsKey(terrain)) terrainCommands[terrain] = new(terrain, sceneCamera);
                VoxelTerrainRenderer.Commands commands = terrainCommands[terrain];

                SceneRender render = SceneRender.Get(terrain);
                if (render.mode == SceneRender.Mode.None) continue;
                int count;
                if (render.mode == SceneRender.Mode.All) {
                    count = VoxelTerrainRenderer.PrepareDraw(terrain, sceneCamera, commands);
                }
                else {
                    VoxelTerrainRenderer renderer = (VoxelTerrainRenderer)render.renderer;
                    count = VoxelTerrainRenderer.PrepareDraw(terrain, renderer.target, commands);
                }

                Graphics.RenderPrimitivesIndexedIndirect(commands.renderParams, MeshTopology.Triangles, voxels.indicesBuffer, commands.commandsBuffer, count);
            }
        }
    }

}
#endif