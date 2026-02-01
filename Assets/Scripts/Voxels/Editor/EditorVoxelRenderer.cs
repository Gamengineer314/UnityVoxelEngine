#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using Voxels.Rendering;
using System.Collections.Generic;

namespace Voxels.Editor {

    [InitializeOnLoad]
    internal static class EditorVoxelRenderer {
        private static readonly Dictionary<VoxelTerrain, VoxelRenderParams> renderParams = new();
        private static VoxelRenderers voxels = null;

        static EditorVoxelRenderer() {
            SceneView.duringSceneGui += EditorRender;
            AssemblyReloadEvents.beforeAssemblyReload += Dispose;
        }

        private static void Dispose() {
            foreach (VoxelRenderParams renderer in renderParams.Values) renderer.Dispose();
            VoxelTerrain[] terrains = Object.FindObjectsOfType<VoxelTerrain>();
            foreach (VoxelTerrain terrain in terrains) terrain.Dispose();
            if (voxels) voxels.Dispose();
        }


        private static void EditorRender(SceneView view) {
            // Init singletons
            if (!VoxelRenderers.Instance) {
                if (voxels) voxels.Dispose();
                voxels = Object.FindObjectOfType<VoxelRenderers>();
                if (!voxels) return;
                voxels.Init();
            }

            Camera sceneCamera = SceneView.currentDrawingSceneView.camera;
            RenderTerrains(sceneCamera);
        }


        private static void RenderTerrains(Camera sceneCamera) {
            VoxelTerrain[] terrains = Object.FindObjectsOfType<VoxelTerrain>();
            foreach (VoxelTerrain terrain in terrains) {
                if (!terrain.Created) continue;
                if (!renderParams.ContainsKey(terrain)) {
                    renderParams[terrain] = new(
                        sceneCamera, VoxelRenderers.Instance.terrainMaterial,
                        terrain.meshesBuffer, terrain.facesBuffer, terrain.colorsBuffer
                    );
                }
                VoxelRenderParams renderParam = renderParams[terrain];

                SceneRender render = SceneRender.Get(terrain);
                if (render.mode == SceneRender.Mode.None) continue;
                if (render.mode == SceneRender.Mode.All) {
                    renderParam.Cull(sceneCamera, VoxelRenderers.Instance.terrainCulling);
                }
                else {
                    VoxelTerrainRenderer terrainRenderer = (VoxelTerrainRenderer)render.renderer;
                    renderParam.Cull(terrainRenderer.target, VoxelRenderers.Instance.terrainCulling);
                }
                renderParam.Render();
            }
        }
    }

}
#endif