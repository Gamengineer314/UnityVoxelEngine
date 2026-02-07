#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using Voxels.Rendering;
using System.Collections.Generic;

namespace Voxels.Editor {

    [InitializeOnLoad]
    internal static class EditorVoxelRenderer {
        private static readonly Dictionary<VoxelTerrain, VoxelRenderParams> terrainRenderParams = new();
        private static ObjectRenderParams objectsRenderParams = null;


        static EditorVoxelRenderer() {
            SceneView.duringSceneGui += EditorRender;
            AssemblyReloadEvents.beforeAssemblyReload += () => {
                foreach (VoxelRenderers renderers in Object.FindObjectsOfType<VoxelRenderers>()) renderers.OnDestroy();
                foreach (VoxelObjects objects in Object.FindObjectsOfType<VoxelObjects>()) objects.OnDestroy();
                foreach (VoxelTerrain terrain in Object.FindObjectsOfType<VoxelTerrain>()) terrain.OnDestroy();
                foreach (VoxelRenderParams renderParams in terrainRenderParams.Values) renderParams.Dispose();
                objectsRenderParams?.Dispose();
            };
            AssemblyReloadEvents.afterAssemblyReload += () => {
                foreach (VoxelRenderers renderers in Object.FindObjectsOfType<VoxelRenderers>()) renderers.Awake();
                foreach (VoxelObjects objects in Object.FindObjectsOfType<VoxelObjects>()) objects.Awake();
            };
        }


        private static void EditorRender(SceneView view) {
            Camera sceneCamera = SceneView.currentDrawingSceneView.camera;
            RenderTerrains(sceneCamera);
            RenderObjects(sceneCamera);
        }


        private static void RenderTerrains(Camera sceneCamera) {
            VoxelTerrain[] terrains = Object.FindObjectsOfType<VoxelTerrain>();
            foreach (VoxelTerrain terrain in terrains) {
                if (!terrain.Created) continue;
                if (!terrainRenderParams.TryGetValue(terrain, out VoxelRenderParams renderParams)) {
                    renderParams = new(
                        sceneCamera, VoxelRenderers.Instance.terrainMaterial,
                        terrain.meshesBuffer, terrain.facesBuffer, terrain.colorsBuffer
                    );
                    terrainRenderParams[terrain] = renderParams;
                }
                if (terrain.meshesBuffer != renderParams.MeshesBuffer) {
                    renderParams.MeshesBuffer = terrain.meshesBuffer;
                    renderParams.FacesBuffer = terrain.facesBuffer;
                    renderParams.ColorsBuffer = terrain.colorsBuffer;
                }
                renderParams.Cull(sceneCamera, VoxelRenderers.Instance.terrainCulling, VoxelRenderers.terrainCullingGroupSize);
                renderParams.Render();
            }
        }


        private static void RenderObjects(Camera sceneCamera) {
            VoxelObjects objects = VoxelObjects.Instance;
            if (!objects) return;
            if (objects.InstanceCount == 0) return;
            objectsRenderParams ??= new(
                sceneCamera, VoxelRenderers.Instance.objectsMaterial,
                objects.MeshesBuffer, objects.FacesBuffer, objects.ColorsBuffer, objects.TransformsBuffer
            );
            if (objects.MeshesBuffer != objectsRenderParams.MeshesBuffer) {
                objectsRenderParams.MeshesBuffer = objects.MeshesBuffer;
                objectsRenderParams.FacesBuffer = objects.FacesBuffer;
                objectsRenderParams.ColorsBuffer = objects.ColorsBuffer;
                objectsRenderParams.TransformsBuffer = objects.TransformsBuffer;
            }
            objectsRenderParams.Cull(sceneCamera, VoxelRenderers.Instance.objectsCulling);
            objectsRenderParams.Render();
        }
    }

}
#endif